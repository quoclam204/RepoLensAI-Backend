using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Contract for retrieving document chunks based on vector semantic similarity (T086).
/// Uses PostgreSQL pgvector cosine distance to find the most relevant chunks for grounding RAG prompts.
/// </summary>
public interface IVectorChunkRetriever
{
    /// <summary>
    /// Retrieves the top-K document chunks most semantically similar to the query embedding,
    /// strictly isolated to the specified analysis run.
    /// </summary>
    /// <param name="analysisId">The analysis run identifier ensuring repository isolation.</param>
    /// <param name="queryEmbedding">The 1536-dimensional query embedding vector.</param>
    /// <param name="topK">The maximum number of nearest chunks to return (default 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of search results ordered by smallest cosine distance / highest similarity.</returns>
    Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
        Guid analysisId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
