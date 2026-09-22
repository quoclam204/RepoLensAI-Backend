namespace RepoLens.Application.DTOs.Analyses;

/// <summary>
/// Status of an analysis run (contracts/api.md Section 7).
/// </summary>
public record AnalysisStatusResponse(
    Guid Id,
    Guid RepositoryId,
    string Status,
    string Stage,
    int Progress,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);
