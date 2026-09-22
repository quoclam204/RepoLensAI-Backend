using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Architecture;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class ArchitectureService : IArchitectureService
{
    private readonly RepoLensDbContext _context;

    public ArchitectureService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<ArchitectureResponse?> GetArchitectureAsync(Guid analysisId, CancellationToken ct = default)
    {
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

        return new ArchitectureResponse(analysis.Id, nodes, edges);
    }
}
