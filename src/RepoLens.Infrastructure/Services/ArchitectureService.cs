using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Architecture;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Architecture query service with safe, deterministic memory caching partitioned strictly by AnalysisId (T063, T111).
/// </summary>
public class ArchitectureService : IArchitectureService
{
    private readonly RepoLensDbContext _context;
    private readonly IMemoryCache? _cache;

    public ArchitectureService(RepoLensDbContext context, IMemoryCache? cache = null)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<ArchitectureResponse?> GetArchitectureAsync(Guid analysisId, CancellationToken ct = default)
    {
        var cacheKey = $"repolens:arch:{analysisId}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out ArchitectureResponse? cached) && cached != null)
        {
            return cached;
        }

        var analysis = await _context.Analyses
            .AsNoTracking()
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

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.AnalysisId == analysisId)
            .ToListAsync(ct);

        var nodes = projects.Select(p => new ArchitectureNodeDto(
            Id: p.Id.ToString(),
            Type: "Project",
            Name: p.Name,
            Path: p.Path,
            Metadata: new { language = p.Language, projectType = p.ProjectType }
        )).ToList();

        var dependencies = await _context.Dependencies
            .AsNoTracking()
            .Include(d => d.Evidence)
            .Where(d => d.AnalysisId == analysisId)
            .ToListAsync(ct);

        var edges = dependencies.Select(d => new ArchitectureEdgeDto(
            Id: d.Id.ToString(),
            Source: d.SourceId,
            Target: d.TargetId,
            Type: d.DependencyType.ToString(),
            Confidence: d.EvidenceId.HasValue ? "confirmed" : "inferred",
            Evidence: d.Evidence != null
                ? new EvidenceSnippetDto(d.Evidence.FilePath, d.Evidence.StartLine, d.Evidence.EndLine)
                : null,
            EvidenceId: d.EvidenceId?.ToString()
        )).ToList();

        var response = new ArchitectureResponse(analysis.Id, nodes, edges);
        _cache?.Set(cacheKey, response, TimeSpan.FromMinutes(10));
        return response;
    }
}
