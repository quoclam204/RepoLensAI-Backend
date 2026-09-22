namespace RepoLens.Application.DTOs.Overview;

/// <summary>
/// Overview response of repository analysis (contracts/api.md Section 9).
/// </summary>
public record AnalysisOverviewResponse(
    Guid AnalysisId,
    RepositoryInfoDto Repository,
    OverviewStatisticsDto Statistics,
    IReadOnlyList<LanguageStatisticDto> Languages);

public record RepositoryInfoDto(
    string Name,
    string SourceType,
    string SourceUrl,
    string? CommitHash);

public record OverviewStatisticsDto(
    int Projects,
    int SourceFiles,
    int Symbols,
    int Dependencies,
    int ApiEndpoints,
    int DatabaseEntities);

public record LanguageStatisticDto(
    string Name,
    int FileCount,
    double Percentage,
    string Support);
