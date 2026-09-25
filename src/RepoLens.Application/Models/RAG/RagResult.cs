using RepoLens.Application.Abstractions.AI.Models;

namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Result of an evidence-grounded RAG query (T087 / FR-009 / NFR-AI-001).
/// Provides the AI explanation along with full source code evidence traceability.
/// </summary>
public sealed record RagResult(
    string Question,
    string Answer,
    IReadOnlyList<VectorChunkSearchResult> RetrievedChunks,
    IReadOnlyList<AiEvidenceItem> Evidence,
    AiConfidenceLevel Confidence,
    bool HasSufficientEvidence,
    string? ContextPrompt = null,
    AnswerValidationResult? Validation = null,
    ConfidenceEvaluationResult? ConfidenceDetails = null);
