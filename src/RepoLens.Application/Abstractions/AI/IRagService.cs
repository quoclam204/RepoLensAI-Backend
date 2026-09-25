using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Abstractions.AI;

/// <summary>
/// Contract for evidence-grounded RAG (Retrieval-Augmented Generation) question answering (T087).
/// Orchestrates the pipeline: Question -> Embed -> Retrieve chunks -> Build context -> AI generation -> Traceable evidence response.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Answers a question about an analyzed repository, strictly grounding the explanation in retrieved code evidence.
    /// </summary>
    /// <param name="request">The RAG request containing analysis ID, question, and top-K parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grounded RAG result linking the AI explanation to source code evidence.</returns>
    Task<RagResult> AnswerQuestionAsync(
        RagRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload to answer a question given explicit parameters.
    /// </summary>
    Task<RagResult> AnswerQuestionAsync(
        Guid analysisId,
        string question,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
