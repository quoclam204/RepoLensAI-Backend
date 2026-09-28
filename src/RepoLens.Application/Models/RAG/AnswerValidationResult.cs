using RepoLens.Application.Abstractions.AI.Models;

namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Status of an answer validation evaluation against retrieved evidence (T088 / NFR-AI-001).
/// </summary>
public enum AnswerValidationStatus
{
    /// <summary>
    /// All asserted claims and citations are fully supported by retrieved repository evidence.
    /// </summary>
    FullySupported = 1,

    /// <summary>
    /// Part of the answer is supported, but one or more claims lacked grounding in the evidence.
    /// </summary>
    PartiallySupported = 2,

    /// <summary>
    /// The answer makes claims that are entirely ungrounded or unsupported by evidence.
    /// </summary>
    Unsupported = 3,

    /// <summary>
    /// No evidence chunks were available to validate or support the answer.
    /// </summary>
    InsufficientEvidence = 4
}

/// <summary>
/// Policy action to apply when an unsupported claim is detected in the answer.
/// </summary>
public enum UnsupportedClaimAction
{
    /// <summary>
    /// Annotate unsupported claims with an uncertainty warning prefix.
    /// </summary>
    MarkUncertain = 1,

    /// <summary>
    /// Remove unsupported claims from the final answer text.
    /// </summary>
    Remove = 2,

    /// <summary>
    /// Replace the entire answer with an insufficient-evidence response if any claim is unsupported.
    /// </summary>
    RejectIfAny = 3
}

/// <summary>
/// Detailed evaluation of an individual claim/statement against retrieved repository evidence.
/// </summary>
public sealed record ClaimValidationItem(
    string ClaimText,
    bool IsSupported,
    string? MatchedFile,
    string? MatchedSymbol,
    string Reason);

/// <summary>
/// Configuration options for AI evidence validation (T088).
/// </summary>
public sealed record AnswerValidationOptions
{
    public static AnswerValidationOptions Default { get; } = new();

    /// <summary>
    /// Action to take on unsupported claims in the answer.
    /// </summary>
    public UnsupportedClaimAction ActionOnUnsupportedClaims { get; init; } = UnsupportedClaimAction.MarkUncertain;

    /// <summary>
    /// Prefix tag used when marking an unsupported claim as uncertain.
    /// </summary>
    public string UncertaintyPrefix { get; init; } = "[Uncertain - unsupported by retrieved evidence]";

    /// <summary>
    /// Standard response when evidence is insufficient or all claims are unsupported.
    /// </summary>
    public string InsufficientEvidenceMessage { get; init; } = InsufficientEvidenceResponse.DefaultMessage;
}

/// <summary>
/// Input request for validating an AI answer against retrieved repository evidence chunks.
/// </summary>
public sealed record AnswerValidationRequest(
    string Answer,
    IReadOnlyList<VectorChunkSearchResult> RetrievedChunks,
    IReadOnlyList<AiEvidenceItem>? AiCitations = null);

/// <summary>
/// Result of validating an AI answer against retrieved repository evidence (T088 / NFR-AI-001).
/// </summary>
public sealed record AnswerValidationResult(
    bool IsValid,
    AnswerValidationStatus Status,
    string ValidatedAnswer,
    IReadOnlyList<AiEvidenceItem> ValidatedEvidence,
    IReadOnlyList<ClaimValidationItem> ClaimDetails,
    IReadOnlyList<string> UnsupportedClaims,
    IReadOnlyList<AiEvidenceItem> RejectedCitations);
