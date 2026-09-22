using RepoLens.Application.DTOs.Architecture;

namespace RepoLens.Application.Abstractions;

public interface IArchitectureService
{
    Task<ArchitectureResponse?> GetArchitectureAsync(Guid analysisId, CancellationToken ct = default);
}
