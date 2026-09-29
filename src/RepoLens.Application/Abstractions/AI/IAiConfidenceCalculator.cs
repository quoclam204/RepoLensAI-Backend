using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Abstractions.AI;

/// <summary>
/// Contract for evaluating evidence-based confidence of an AI-generated answer (T089 / NFR-AI-001).
/// Enforces that confidence is grounded strictly in repository evidence quality, retrieval metrics,
/// and answer validation status.
/// </summary>
public interface IAiConfidenceCalculator
{
    /// <summary>
    /// Computes the explainable evidence-based confidence evaluation for an AI answer.
    /// </summary>
    /// <param name="request">The confidence evaluation request containing question, answer, chunks, and validation results.</param>
    /// <returns>A structured confidence evaluation result including level, score, rationale, and factor breakdown.</returns>
    ConfidenceEvaluationResult EvaluateConfidence(ConfidenceEvaluationRequest request);

    /// <summary>
    /// Asynchronously computes the explainable evidence-based confidence evaluation for an AI answer.
    /// </summary>
    Task<ConfidenceEvaluationResult> EvaluateConfidenceAsync(
        ConfidenceEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
