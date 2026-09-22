using RepoLens.Application.DTOs.Database;

namespace RepoLens.Application.Abstractions;

public interface IDatabaseModelService
{
    Task<DatabaseModelResponse?> GetDatabaseModelAsync(Guid analysisId, CancellationToken ct = default);
    Task<DatabaseEntityDetailResponse?> GetEntityDetailAsync(Guid analysisId, Guid entityId, CancellationToken ct = default);
}
