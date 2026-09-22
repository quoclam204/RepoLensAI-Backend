using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Analyses;
using RepoLens.Application.DTOs.Overview;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;
using AnalysisEntity = RepoLens.Domain.Entities.Analysis;

namespace RepoLens.Infrastructure.Services;

public class AnalysisService : IAnalysisService
{
    private readonly RepoLensDbContext _context;
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(RepoLensDbContext context, ILogger<AnalysisService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CreateAnalysisResponse> CreateAnalysisFromGitAsync(CreateAnalysisGitRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating analysis for Git repository: {SourceUrl}", request.SourceUrl);

        var repoName = ExtractRepoNameFromUrl(request.SourceUrl);

        var repository = await _context.Repositories
            .FirstOrDefaultAsync(r => r.SourceLocation == request.SourceUrl && r.SourceType == RepositorySourceType.GitUrl, ct);

        if (repository == null)
        {
            repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = repoName,
                SourceType = RepositorySourceType.GitUrl,
                SourceLocation = request.SourceUrl,
                Status = RepositoryStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _context.Repositories.Add(repository);
        }

        var analysis = new AnalysisEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            Status = AnalysisStatus.Created,
            CurrentStage = "Validation",
            StartedAt = DateTimeOffset.UtcNow
        };

        _context.Analyses.Add(analysis);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Analysis {AnalysisId} created successfully for repository {RepositoryId}", analysis.Id, repository.Id);

        return new CreateAnalysisResponse(
            analysis.Id,
            repository.Id,
            analysis.Status.ToString(),
            analysis.StartedAt);
    }

    public async Task<CreateAnalysisResponse> CreateAnalysisFromZipAsync(string fileName, Stream contentStream, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating analysis for ZIP file: {FileName}", fileName);

        var repoName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(repoName))
        {
            repoName = "uploaded-archive";
        }

        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = repoName,
            SourceType = RepositorySourceType.ZipUpload,
            SourceLocation = fileName,
            Status = RepositoryStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Repositories.Add(repository);

        var analysis = new AnalysisEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            Status = AnalysisStatus.Created,
            CurrentStage = "Validation",
            StartedAt = DateTimeOffset.UtcNow
        };

        _context.Analyses.Add(analysis);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Analysis {AnalysisId} created successfully for ZIP upload {FileName}", analysis.Id, fileName);

        return new CreateAnalysisResponse(
            analysis.Id,
            repository.Id,
            analysis.Status.ToString(),
            analysis.StartedAt);
    }

    public async Task<AnalysisStatusResponse?> GetAnalysisStatusAsync(Guid analysisId, CancellationToken ct = default)
    {
        var analysis = await _context.Analyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

        if (analysis == null)
        {
            return null;
        }

        var progress = CalculateProgress(analysis.Status, analysis.CurrentStage);

        return new AnalysisStatusResponse(
            analysis.Id,
            analysis.RepositoryId,
            analysis.Status.ToString(),
            analysis.CurrentStage,
            progress,
            analysis.StartedAt,
            analysis.CompletedAt,
            analysis.Error);
    }

    public async Task<AnalysisOverviewResponse?> GetAnalysisOverviewAsync(Guid analysisId, CancellationToken ct = default)
    {
        var analysis = await _context.Analyses
            .AsNoTracking()
            .Include(a => a.Repository)
            .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

        if (analysis == null)
        {
            return null;
        }

        if (analysis.Status == AnalysisStatus.Failed)
        {
            throw new AnalysisFailedException(analysis.CurrentStage);
        }

        if (analysis.Status != AnalysisStatus.Completed)
        {
            throw new AnalysisNotReadyException(analysis.Status.ToString());
        }

        var projectCount = await _context.Projects
            .AsNoTracking()
            .CountAsync(p => p.AnalysisId == analysisId, ct);

        var sourceFileCount = await _context.SourceFiles
            .AsNoTracking()
            .CountAsync(f => f.AnalysisId == analysisId, ct);

        var symbolCount = await _context.CodeSymbols
            .AsNoTracking()
            .CountAsync(s => s.SourceFile.AnalysisId == analysisId, ct);

        var dependencyCount = await _context.Dependencies
            .AsNoTracking()
            .CountAsync(d => d.AnalysisId == analysisId, ct);

        var endpointCount = await _context.ApiEndpoints
            .AsNoTracking()
            .CountAsync(e => e.AnalysisId == analysisId, ct);

        var databaseEntityCount = await _context.DatabaseEntities
            .AsNoTracking()
            .CountAsync(e => e.AnalysisId == analysisId, ct);

        var languageGroups = await _context.SourceFiles
            .AsNoTracking()
            .Where(f => f.AnalysisId == analysisId && !string.IsNullOrEmpty(f.Language))
            .GroupBy(f => f.Language)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalFilesWithLanguage = languageGroups.Sum(g => g.Count);
        var languages = languageGroups.Select(g => new LanguageStatisticDto(
            g.Language,
            g.Count,
            totalFilesWithLanguage > 0 ? Math.Round((double)g.Count / totalFilesWithLanguage * 100, 2) : 0,
            GetLanguageSupport(g.Language)
        )).ToList();

        var repoDto = new RepositoryInfoDto(
            analysis.Repository.Name,
            analysis.Repository.SourceType.ToString(),
            analysis.Repository.SourceLocation,
            analysis.CommitHash);

        var statsDto = new OverviewStatisticsDto(
            projectCount,
            sourceFileCount,
            symbolCount,
            dependencyCount,
            endpointCount,
            databaseEntityCount);

        return new AnalysisOverviewResponse(
            analysis.Id,
            repoDto,
            statsDto,
            languages);
    }

    private static int CalculateProgress(AnalysisStatus status, string stage)
    {
        if (status == AnalysisStatus.Completed) return 100;
        if (status == AnalysisStatus.Failed) return 0;

        return stage switch
        {
            "Validation" => 5,
            "RepositoryAcquisition" => 15,
            "FileScanning" => 30,
            "LanguageDetection" => 40,
            "ProjectDetection" => 50,
            "StaticAnalysis" => 65,
            "DependencyAnalysis" => 75,
            "ApiAnalysis" => 80,
            "DatabaseAnalysis" => 85,
            "EvidenceGeneration" => 90,
            "Persistence" => 95,
            "Completed" => 100,
            _ => 10
        };
    }

    private static string GetLanguageSupport(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "c#" or "csharp" => "Full",
            "typescript" or "javascript" => "Partial",
            _ => "Unsupported"
        };
    }

    private static string ExtractRepoNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimEnd('/');
            var name = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrWhiteSpace(name) ? "repository" : name;
        }
        catch
        {
            return "repository";
        }
    }
}
