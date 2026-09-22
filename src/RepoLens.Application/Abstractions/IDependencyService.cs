using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Dependencies;

namespace RepoLens.Application.Abstractions;

public interface IDependencyService
{
    Task<PagedResult<DependencyItemDto>> GetDependenciesAsync(Guid analysisId, DependencyFilterParams filter, CancellationToken ct = default);
    Task<DependencyDetailResponse?> GetDependencyDetailAsync(Guid analysisId, Guid dependencyId, CancellationToken ct = default);
}
