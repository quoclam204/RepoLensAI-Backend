namespace RepoLens.Application.DTOs.Analyses;

/// <summary>
/// Response when an analysis is created and accepted for processing (contracts/api.md Section 6.3).
/// </summary>
public record CreateAnalysisResponse(
    Guid AnalysisId,
    Guid RepositoryId,
    string Status,
    DateTimeOffset CreatedAt);
