using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Evidence;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class EvidenceService : IEvidenceService
{
    private readonly RepoLensDbContext _context;

    public EvidenceService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EvidenceDetailResponse>> GetEvidencesAsync(Guid analysisId, EvidenceFilterParams filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _context.Evidences
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId);

        if (!string.IsNullOrWhiteSpace(filter.FilePath))
        {
            query = query.Where(e => e.FilePath.ToLower().Contains(filter.FilePath.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(filter.Symbol))
        {
            query = query.Where(e => e.Symbol != null && e.Symbol.ToLower().Contains(filter.Symbol.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(filter.Type) && Enum.TryParse<EvidenceType>(filter.Type, true, out var evType))
        {
            query = query.Where(e => e.EvidenceType == evType);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.FilePath)
            .ThenBy(e => e.StartLine)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var resultItems = items.Select(e => new EvidenceDetailResponse(
            Id: e.Id.ToString(),
            AnalysisId: e.AnalysisId.ToString(),
            FilePath: e.FilePath,
            Symbol: e.Symbol,
            StartLine: e.StartLine,
            EndLine: e.EndLine,
            EvidenceType: e.EvidenceType.ToString(),
            Description: e.Description
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<EvidenceDetailResponse>(resultItems, totalCount, page, pageSize, totalPages);
    }

    public async Task<EvidenceDetailResponse?> GetEvidenceDetailAsync(Guid analysisId, Guid evidenceId, CancellationToken ct = default)
    {
        var evidence = await _context.Evidences
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AnalysisId == analysisId && e.Id == evidenceId, ct);

        if (evidence == null)
        {
            return null;
        }

        return new EvidenceDetailResponse(
            Id: evidence.Id.ToString(),
            AnalysisId: evidence.AnalysisId.ToString(),
            FilePath: evidence.FilePath,
            Symbol: evidence.Symbol,
            StartLine: evidence.StartLine,
            EndLine: evidence.EndLine,
            EvidenceType: evidence.EvidenceType.ToString(),
            Description: evidence.Description
        );
    }
}
