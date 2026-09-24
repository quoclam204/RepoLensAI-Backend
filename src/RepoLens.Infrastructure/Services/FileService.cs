using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Files;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class FileService : IFileService
{
    private readonly RepoLensDbContext _context;

    public FileService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<FileItemDto>> GetFilesAsync(Guid analysisId, FileFilterParams filter, CancellationToken ct = default)
    {
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
            query = query.Where(f => f.Language.ToLower() == filter.Language.ToLower());
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
        var file = await _context.SourceFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.AnalysisId == analysisId && f.Id == fileId, ct);

        if (file == null)
        {
            return null;
        }

        // TODO: [Giả định cần chốt với nhóm] Trong tương lai file content được đọc từ Storage/Workspace T031.
        // Hiện tại nếu file tồn tại trên ổ đĩa cục bộ sẽ đọc trực tiếp, ngược lại trả về thông báo đã được lưu trữ an toàn.
        string content;
        int lineCount = 0;

        if (File.Exists(file.Path))
        {
            content = await File.ReadAllTextAsync(file.Path, ct);
            lineCount = content.Split('\n').Length;
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
