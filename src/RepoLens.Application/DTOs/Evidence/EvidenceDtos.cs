namespace RepoLens.Application.DTOs.Evidence;

public record EvidenceFilterParams
{
    public string? FilePath { get; init; }
    public string? Symbol { get; init; }
    public string? Type { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record EvidenceDetailResponse(
    string Id,
    string AnalysisId,
    string FilePath,
    string? Symbol,
    int StartLine,
    int EndLine,
    string EvidenceType,
    string Description);
