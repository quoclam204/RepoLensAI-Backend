using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Evidence;

namespace RepoLens.Application.Abstractions;

public interface IEvidenceService
{
    Task<PagedResult<EvidenceDetailResponse>> GetEvidencesAsync(Guid analysisId, EvidenceFilterParams filter, CancellationToken ct = default);
    Task<EvidenceDetailResponse?> GetEvidenceDetailAsync(Guid analysisId, Guid evidenceId, CancellationToken ct = default);
}
