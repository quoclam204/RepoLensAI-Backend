namespace RepoLens.Application.Abstractions.AI.Models;

public sealed record AiEvidenceItem
{
    public required string File { get; init; }
    public string? Symbol { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public string? Reason { get; init; }
}
