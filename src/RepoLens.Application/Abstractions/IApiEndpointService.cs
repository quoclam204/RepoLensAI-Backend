using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Endpoints;

namespace RepoLens.Application.Abstractions;

public interface IApiEndpointService
{
    Task<PagedResult<EndpointItemDto>> GetEndpointsAsync(Guid analysisId, EndpointFilterParams filter, CancellationToken ct = default);
    Task<EndpointDetailResponse?> GetEndpointDetailAsync(Guid analysisId, Guid endpointId, CancellationToken ct = default);
}
