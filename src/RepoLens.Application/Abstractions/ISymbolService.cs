using RepoLens.Application.DTOs.Symbols;

namespace RepoLens.Application.Abstractions;

public interface ISymbolService
{
    Task<SymbolDetailResponse?> GetSymbolDetailAsync(Guid analysisId, Guid symbolId, CancellationToken ct = default);
}
