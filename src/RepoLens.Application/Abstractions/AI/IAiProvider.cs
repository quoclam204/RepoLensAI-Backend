using RepoLens.Application.Abstractions.AI.Models;

namespace RepoLens.Application.Abstractions.AI;

public interface IAiProvider
{
    Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken = default);
}
