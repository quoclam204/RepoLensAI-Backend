using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Abstractions.AI;

/// <summary>
/// Contract for validating that AI-generated answers and claims are adequately grounded in retrieved repository evidence (T088 / NFR-AI-001).
/// Enforces that unsupported claims are marked uncertain, removed, or result in an insufficient-evidence response,
/// and rejects hallucinated citations.
/// </summary>
public interface IAiEvidenceValidator
{
    /// <summary>
    /// Validates an AI-generated answer against the retrieved repository evidence chunks.
    /// </summary>
    /// <param name="request">The validation request containing the answer, retrieved chunks, and optional AI citations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result detailing claim grounding status, validated text, and verified evidence items.</returns>
    Task<AnswerValidationResult> ValidateAnswerAsync(
        AnswerValidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload to validate an answer with explicit arguments.
    /// </summary>
    Task<AnswerValidationResult> ValidateAnswerAsync(
        string answer,
        IReadOnlyList<VectorChunkSearchResult> retrievedChunks,
        IReadOnlyList<AiEvidenceItem>? aiCitations = null,
        CancellationToken cancellationToken = default);
}
