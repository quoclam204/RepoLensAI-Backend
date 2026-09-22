namespace RepoLens.Application.DTOs.Files;

public record FileFilterParams
{
    public string? Path { get; init; }
    public string? Language { get; init; }
    public string? ProjectId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record FileItemDto(
    string Id,
    string Path,
    string Language,
    string ProjectId,
    long Size,
    string AnalysisStatus);

public record FileDetailResponse(
    string Id,
    string Path,
    string Language,
    string ProjectId,
    long Size,
    IReadOnlyList<FileSymbolDto> Symbols);

public record FileSymbolDto(
    string Id,
    string Name,
    string FullName,
    string Type,
    int StartLine,
    int EndLine);

public record FileContentResponse(
    string FileId,
    string Path,
    string Language,
    string Content,
    int LineCount);
