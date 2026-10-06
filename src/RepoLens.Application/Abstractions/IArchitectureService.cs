using RepoLens.Application.DTOs.Architecture;

namespace RepoLens.Application.Abstractions;

public interface IArchitectureService
{
    Task<ArchitectureResponse?> GetArchitectureAsync(Guid analysisId, CancellationToken ct = default);
    Task<ArchitectureTraceResponse?> TracePathAsync(Guid analysisId, string fromNodeId, string toNodeId, CancellationToken ct = default);
}

