namespace RepoLens.Application.Abstractions.AI.Models;

public sealed record AiRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public string? Context { get; init; }
}
