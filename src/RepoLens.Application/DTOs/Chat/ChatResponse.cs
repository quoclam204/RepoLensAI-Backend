namespace RepoLens.Application.DTOs.Chat;

/// <summary>
/// Response body for the Chat API (T091 / FR-009 / contracts/api.md Sections 26-29).
/// </summary>
public sealed record ChatResponse
{
    /// <summary>
    /// The AI-generated answer grounded in repository evidence.
    /// When evidence is insufficient, this is the canonical T090 insufficient-evidence message.
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// Confidence level based on available evidence (contracts/api.md Section 27).
    /// Allowed values: high, medium, low, unknown. Never a statistical probability.
    /// </summary>
    public required string Confidence { get; init; }

    /// <summary>
    /// Supporting evidence traceability (contracts/api.md Section 28).
    /// Empty when the answer is the insufficient-evidence response.
    /// </summary>
    public IReadOnlyList<ChatEvidenceItem> Evidence { get; init; } = [];
}

/// <summary>
/// Individual evidence item referenced in a chat response (contracts/api.md Section 28).
/// </summary>
public sealed record ChatEvidenceItem
{
    /// <summary>
    /// The file path where the evidence is located.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The code symbol (method, class, etc.) associated with the evidence, if any.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// The start line of the evidence chunk in the source file.
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// The end line of the evidence chunk in the source file.
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// A brief reason why this evidence supports the answer.
    /// </summary>
    public string? Reason { get; init; }
}
