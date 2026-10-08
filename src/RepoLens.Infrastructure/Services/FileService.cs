using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Files;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.Infrastructure.Services;

public class FileService : IFileService
{
    private readonly RepoLensDbContext _context;
    private readonly ITemporaryWorkspaceManager? _workspaceManager;
    private readonly WorkspaceOptions _workspaceOptions;
    private readonly ILogger<FileService>? _logger;

    public FileService(
        RepoLensDbContext context,
        ITemporaryWorkspaceManager? workspaceManager = null,
        IOptions<WorkspaceOptions>? workspaceOptions = null,
        ILogger<FileService>? logger = null)
    {
        _context = context;
        _workspaceManager = workspaceManager;
        _workspaceOptions = workspaceOptions?.Value ?? new WorkspaceOptions();
        _logger = logger;
    }

    public async Task<PagedResult<FileItemDto>> GetFilesAsync(Guid analysisId, FileFilterParams filter, CancellationToken ct = default)
    {
        await AnalysisValidationHelper.EnsureAnalysisCompletedAsync(_context, analysisId, ct);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _context.SourceFiles
            .AsNoTracking()
            .Where(f => f.AnalysisId == analysisId);

        if (!string.IsNullOrWhiteSpace(filter.Path))
        {
            query = query.Where(f => f.Path.ToLower().Contains(filter.Path.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(filter.Language))
        {
            var lang = filter.Language.Trim().ToLower();
            if (lang == "c#" || lang == "csharp" || lang == "cs")
            {
                query = query.Where(f => f.Language.ToLower() == "csharp" || f.Language.ToLower() == "cs" || f.Path.EndsWith(".cs"));
            }
            else if (lang == "typescript" || lang == "ts")
            {
                query = query.Where(f => f.Language.ToLower() == "typescript" || f.Language.ToLower() == "ts" || f.Path.EndsWith(".ts") || f.Path.EndsWith(".tsx"));
            }
            else if (lang == "javascript" || lang == "js")
            {
                query = query.Where(f => f.Language.ToLower() == "javascript" || f.Language.ToLower() == "js" || f.Path.EndsWith(".js") || f.Path.EndsWith(".jsx"));
            }
            else
            {
                query = query.Where(f => f.Language.ToLower() == lang || f.Path.ToLower().EndsWith("." + lang));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.ProjectId) && Guid.TryParse(filter.ProjectId, out var projId))
        {
            query = query.Where(f => f.ProjectId == projId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(f => f.Path.ToLower().Contains(filter.Search.ToLower()));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(f => f.Path)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var resultItems = items.Select(f => new FileItemDto(
            Id: f.Id.ToString(),
            Path: f.Path,
            Language: f.Language,
            ProjectId: f.ProjectId.ToString(),
            Size: f.Size,
            AnalysisStatus: f.AnalysisStatus.ToString()
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<FileItemDto>(resultItems, totalCount, page, pageSize, totalPages);
    }

    public async Task<FileDetailResponse?> GetFileDetailAsync(Guid analysisId, Guid fileId, CancellationToken ct = default)
    {
        await AnalysisValidationHelper.EnsureAnalysisCompletedAsync(_context, analysisId, ct);

        var file = await _context.SourceFiles
            .AsNoTracking()
            .Include(f => f.Symbols)
            .FirstOrDefaultAsync(f => f.AnalysisId == analysisId && f.Id == fileId, ct);

        if (file == null)
        {
            return null;
        }

        var symbols = file.Symbols.Select(s => new FileSymbolDto(
            Id: s.Id.ToString(),
            Name: s.Name,
            FullName: s.FullName,
            Type: s.SymbolType.ToString(),
            StartLine: s.StartLine,
            EndLine: s.EndLine
        )).ToList();

        return new FileDetailResponse(
            Id: file.Id.ToString(),
            Path: file.Path,
            Language: file.Language,
            ProjectId: file.ProjectId.ToString(),
            Size: file.Size,
            Symbols: symbols
        );
    }

    public async Task<FileContentResponse?> GetFileContentAsync(Guid analysisId, Guid fileId, CancellationToken ct = default)
    {
        await AnalysisValidationHelper.EnsureAnalysisCompletedAsync(_context, analysisId, ct);

        var file = await _context.SourceFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.AnalysisId == analysisId && f.Id == fileId, ct);

        if (file == null)
        {
            return null;
        }

        string content;
        int lineCount = 0;

        string? resolvedPath = null;
        if (_workspaceManager != null)
        {
            var workspace = await _workspaceManager.GetWorkspaceAsync(analysisId, ct);
            if (workspace != null && !string.IsNullOrWhiteSpace(workspace.RootPath))
            {
                var candidate = Path.Combine(workspace.RootPath, file.Path);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                }
            }
        }

        if (resolvedPath == null && !string.IsNullOrWhiteSpace(_workspaceOptions.BaseDirectory))
        {
            var candidate = Path.Combine(_workspaceOptions.BaseDirectory, analysisId.ToString("D"), file.Path);
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
            }
        }

        if (resolvedPath == null && File.Exists(file.Path))
        {
            resolvedPath = file.Path;
        }

        if (resolvedPath != null && File.Exists(resolvedPath))
        {
            try
            {
                content = await File.ReadAllTextAsync(resolvedPath, ct);
                lineCount = content.Split('\n').Length;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read content for file {FilePath} at {ResolvedPath}", file.Path, resolvedPath);
                content = $"// Error reading file content from {file.Path}: {ex.Message}";
                lineCount = 1;
            }
        }
        else
        {
            content = $"// Source code content for {file.Path} (Stored in repository workspace)";
            lineCount = 1;
        }

        return new FileContentResponse(
            FileId: file.Id.ToString(),
            Path: file.Path,
            Language: file.Language,
            Content: content,
            LineCount: lineCount
        );
    }
}
