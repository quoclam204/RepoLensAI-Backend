namespace RepoLens.Application.DTOs.Dependencies;

public record DependencyFilterParams
{
    public string? ProjectId { get; init; }
    public string? Type { get; init; }
    public string? Direction { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record DependencyItemDto(
    string Id,
    DependencyNodeDto Source,
    DependencyNodeDto Target,
    string Type,
    string? EvidenceId = null);

public record DependencyNodeDto(
    string Id,
    string Name,
    string Type);

public record DependencyDetailResponse(
    string Id,
    string Type,
    DependencyRefDto Source,
    DependencyRefDto Target,
    IReadOnlyList<DependencyEvidenceDto> Evidence);

public record DependencyRefDto(
    string Id,
    string Name);

public record DependencyEvidenceDto(
    string Id,
    string File,
    int StartLine,
    int EndLine,
    string Description);
