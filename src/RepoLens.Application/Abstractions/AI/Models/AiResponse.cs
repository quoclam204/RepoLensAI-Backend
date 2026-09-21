namespace RepoLens.Application.Abstractions.AI.Models;

public sealed record AiResponse
{
    public required string Answer { get; init; }
    public AiConfidenceLevel Confidence { get; init; } = AiConfidenceLevel.Unknown;
    public IReadOnlyList<AiEvidenceItem> Evidence { get; init; } = [];
}
