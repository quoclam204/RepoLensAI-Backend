using RepoLens.Application.Abstractions.AI.Models;

namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Detailed breakdown of the evidence factors that contributed to the confidence score (T089 / NFR-AI-001).
/// </summary>
public sealed record ConfidenceFactorBreakdown(
    float GroundingFactor,
    float RetrievalSimilarityFactor,
    float StaticEvidenceFactor,
    float CoverageFactor);

/// <summary>
/// Input parameters for evaluating evidence-based confidence of an AI answer (T089 / NFR-AI-001).
/// </summary>
public sealed record ConfidenceEvaluationRequest(
    string Question,
    string Answer,
    IReadOnlyList<VectorChunkSearchResult> RetrievedChunks,
    AnswerValidationResult? ValidationResult = null,
    IReadOnlyList<AiEvidenceItem>? Evidence = null);

/// <summary>
/// Evaluation result of the evidence-grounded confidence calculation (T089 / NFR-AI-001).
/// Provides an explainable, deterministic confidence level and score backed by repository evidence.
/// </summary>
public sealed record ConfidenceEvaluationResult(
    AiConfidenceLevel Level,
    float Score,
    string Rationale,
    ConfidenceFactorBreakdown Factors);
