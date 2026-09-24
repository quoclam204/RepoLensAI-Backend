using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Contract for retrieving verified source code evidence to ground RAG prompts and prevent LLM hallucinations.
/// </summary>
public interface IEvidenceRetriever
{
    /// <summary>
    /// Retrieves verified source evidences matching symbol or file query for an analysis run.
    /// If no valid evidence is found or confidence is below threshold, returns HasSufficientEvidence = false.
    /// </summary>
    Task<GroundedRetrievalResult> RetrieveGroundedEvidenceAsync(
        Guid analysisId,
        string question,
        string? targetSymbolOrPath = null,
        float minimumConfidence = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves verified document chunks for an analysis run, optionally filtering by symbol/file path or content keyword.
    /// Preserves full provenance: file path, line spans, evidence id, and confidence score.
    /// </summary>
    Task<IReadOnlyList<RetrievedChunkItem>> RetrieveDocumentChunksAsync(
        Guid analysisId,
        string? targetSymbolOrPath = null,
        string? keyword = null,
        float minimumConfidence = 0.5f,
        CancellationToken cancellationToken = default);
}
