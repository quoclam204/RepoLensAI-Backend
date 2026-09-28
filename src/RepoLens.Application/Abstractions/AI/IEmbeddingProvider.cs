namespace RepoLens.Application.Abstractions.AI;

public interface IEmbeddingProvider
{
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}
