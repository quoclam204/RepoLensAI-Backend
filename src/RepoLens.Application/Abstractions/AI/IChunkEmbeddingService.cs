using RepoLens.Application.DTOs.Persistence;

namespace RepoLens.Application.Abstractions.AI;

/// <summary>
/// Embedding generation status for T085 (FR-009).
/// </summary>
public enum ChunkEmbeddingStatus
{
    AllSucceeded,
    NoEligibleChunks,
    PartialFailure,
    CompleteFailure
}

/// <summary>
/// Implementation options for chunk embedding (T085).
/// Values are implementation defaults, not SRS requirements.
/// </summary>
public sealed record ChunkEmbeddingOptions
{
    public static ChunkEmbeddingOptions Default { get; } = new();

    /// <summary>
    /// Required vector dimension. Must match the T084 pgvector column (vector(1536)).
    /// </summary>
    public int EmbeddingDimension { get; init; } = 1536;

    /// <summary>
    /// Maximum number of chunk texts sent to IEmbeddingProvider per call.
    /// </summary>
    public int MaxBatchSize { get; init; } = 32;
}

/// <summary>
/// Explicit result of a T085 embedding operation.
/// </summary>
public sealed record ChunkEmbeddingResult(
    ChunkEmbeddingStatus Status,
    int EligibleCount,
    int EmbeddedCount,
    IReadOnlyList<string> Errors)
{
    public IReadOnlyList<string> AllErrors => Errors;
}

/// <summary>
/// Application service that generates embeddings for already-eligible document chunks (T085).
/// Eligible means: chunk exists in the T083 output (AnalysisResultModel.DocumentChunks)
/// with non-null/non-whitespace content. T083 (DocumentChunkGenerator) already produces
/// safety-processed/masked content; this service performs no masking, no approval
/// authorization, and no persistence. The repository currently has no separate approval
/// store, so T085 relies on the existing T083 safety/masking boundary.
/// </summary>
public interface IChunkEmbeddingService
{
    /// <summary>
    /// Generates embeddings for eligible chunks and returns a new AnalysisResultModel
    /// with validated vectors assigned. The input model is not mutated.
    /// </summary>
    Task<(AnalysisResultModel Result, ChunkEmbeddingResult Outcome)> PopulateEmbeddingsAsync(
        AnalysisResultModel result,
        CancellationToken cancellationToken = default);
}
