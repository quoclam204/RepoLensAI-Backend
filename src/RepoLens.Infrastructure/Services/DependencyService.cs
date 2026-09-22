using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Dependencies;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class DependencyService : IDependencyService
{
    private readonly RepoLensDbContext _context;

    public DependencyService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<DependencyItemDto>> GetDependenciesAsync(Guid analysisId, DependencyFilterParams filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId);

        if (!string.IsNullOrWhiteSpace(filter.ProjectId))
        {
            if (string.Equals(filter.Direction, "inbound", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(d => d.TargetId == filter.ProjectId);
            }
            else if (string.Equals(filter.Direction, "outbound", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(d => d.SourceId == filter.ProjectId);
            }
            else
            {
                query = query.Where(d => d.SourceId == filter.ProjectId || d.TargetId == filter.ProjectId);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Type) && Enum.TryParse<DependencyType>(filter.Type, true, out var depType))
        {
            query = query.Where(d => d.DependencyType == depType);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Preload project dictionary for this analysis to resolve names
        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.AnalysisId == analysisId)
            .ToDictionaryAsync(p => p.Id.ToString(), p => p.Name, ct);

        var resultItems = items.Select(d =>
        {
            var sourceName = projects.TryGetValue(d.SourceId, out var sName) ? sName : d.SourceId;
            var targetName = projects.TryGetValue(d.TargetId, out var tName) ? tName : d.TargetId;

            return new DependencyItemDto(
                d.Id.ToString(),
                new DependencyNodeDto(d.SourceId, sourceName, "Project"),
                new DependencyNodeDto(d.TargetId, targetName, "Project"),
                d.DependencyType.ToString(),
                d.EvidenceId?.ToString());
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<DependencyItemDto>(resultItems, totalCount, page, pageSize, totalPages);
    }

    public async Task<DependencyDetailResponse?> GetDependencyDetailAsync(Guid analysisId, Guid dependencyId, CancellationToken ct = default)
    {
        var dependency = await _context.Dependencies
            .AsNoTracking()
            .Include(d => d.Evidence)
            .FirstOrDefaultAsync(d => d.AnalysisId == analysisId && d.Id == dependencyId, ct);

        if (dependency == null)
        {
            return null;
        }

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.AnalysisId == analysisId)
            .ToDictionaryAsync(p => p.Id.ToString(), p => p.Name, ct);

        var sourceName = projects.TryGetValue(dependency.SourceId, out var sName) ? sName : dependency.SourceId;
        var targetName = projects.TryGetValue(dependency.TargetId, out var tName) ? tName : dependency.TargetId;

        var evidenceList = new List<DependencyEvidenceDto>();
        if (dependency.Evidence != null)
        {
            evidenceList.Add(new DependencyEvidenceDto(
                dependency.Evidence.Id.ToString(),
                dependency.Evidence.FilePath,
                dependency.Evidence.StartLine,
                dependency.Evidence.EndLine,
                dependency.Evidence.Description));
        }

        return new DependencyDetailResponse(
            dependency.Id.ToString(),
            dependency.DependencyType.ToString(),
            new DependencyRefDto(dependency.SourceId, sourceName),
            new DependencyRefDto(dependency.TargetId, targetName),
            evidenceList);
    }
}
