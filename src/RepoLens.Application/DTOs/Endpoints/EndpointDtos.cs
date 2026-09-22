namespace RepoLens.Application.DTOs.Endpoints;

public record EndpointFilterParams
{
    public string? Method { get; init; }
    public string? Route { get; init; }
    public string? ProjectId { get; init; }
    public string? Controller { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record EndpointItemDto(
    string Id,
    string Method,
    string Route,
    ProjectRefDto Project,
    string? Controller,
    string? Action,
    string? SymbolId = null,
    string? EvidenceId = null);

public record ProjectRefDto(
    string Id,
    string Name);

public record EndpointDetailResponse(
    string Id,
    string Method,
    string Route,
    string? Controller,
    string? Action,
    string Project,
    SourceRefDto Source,
    IReadOnlyList<EndpointEvidenceSnippetDto> Evidence);

public record SourceRefDto(
    string File,
    string? Symbol);

public record EndpointEvidenceSnippetDto(
    string File,
    int StartLine,
    int EndLine,
    string Reason);
