namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Standard contract for "Insufficient Evidence" responses (T090 / NFR-AI-001, FR-024).
/// When repository evidence is insufficient to answer a question, the system MUST return
/// the canonical message and MUST NOT fabricate files, classes, methods, endpoints,
/// database schemas, or dependencies. Absence of evidence must never become a factual answer.
/// </summary>
public static class InsufficientEvidenceResponse
{
    /// <summary>
    /// Canonical insufficient-evidence message required by T090.
    /// </summary>
    public const string DefaultMessage = "Insufficient evidence in the analyzed repository.";

    private static readonly string[] AcknowledgmentPhrases =
    [
        "insufficient evidence",
        "no relevant evidence",
        "no evidence found",
        "cannot find any evidence"
    ];

    /// <summary>
    /// Returns true when the answer text honestly acknowledges a lack of repository evidence
    /// instead of asserting unverified repository facts.
    /// </summary>
    public static bool ContainsInsufficientEvidenceAcknowledgment(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        return AcknowledgmentPhrases.Any(phrase =>
            answer.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true when a T088 validation outcome requires the pipeline to surface
    /// the insufficient-evidence response: no evidence was available
    /// (<see cref="AnswerValidationStatus.InsufficientEvidence"/>) or every asserted
    /// claim was ungrounded (<see cref="AnswerValidationStatus.Unsupported"/>).
    /// </summary>
    public static bool RequiresInsufficientEvidenceResponse(AnswerValidationResult? validationResult)
    {
        return validationResult is not null &&
               (validationResult.Status == AnswerValidationStatus.InsufficientEvidence ||
                validationResult.Status == AnswerValidationStatus.Unsupported);
    }
}
