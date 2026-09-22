namespace RepoLens.Application.DTOs.Symbols;

public record SymbolDetailResponse(
    string Id,
    string Name,
    string FullName,
    string Type,
    SymbolFileRefDto File,
    int StartLine,
    int EndLine,
    IReadOnlyList<SymbolRelationshipDto> Relationships);

public record SymbolFileRefDto(
    string Id,
    string Path);

public record SymbolRelationshipDto(
    string Type,
    string TargetSymbolId);
