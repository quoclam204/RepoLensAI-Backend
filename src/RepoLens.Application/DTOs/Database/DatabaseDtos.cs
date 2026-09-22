namespace RepoLens.Application.DTOs.Database;

public record DatabaseModelResponse(
    IReadOnlyList<DatabaseEntityDto> Entities,
    IReadOnlyList<DatabaseRelationshipDto> Relationships);

public record DatabaseEntityDto(
    string Id,
    string Name,
    string Type,
    string? SourceSymbolId,
    IReadOnlyList<EntityPropertyDto> Properties);

public record EntityPropertyDto(
    string Name,
    string Type,
    bool Nullable);

public record DatabaseRelationshipDto(
    string Id,
    string SourceEntityId,
    string TargetEntityId,
    string Type,
    string Confidence,
    string? EvidenceId = null);

public record DatabaseEntityDetailResponse(
    string Id,
    string Name,
    string Type,
    DatabaseSourceRefDto? Source,
    IReadOnlyList<EntityPropertyDto> Properties,
    IReadOnlyList<DatabaseRelationshipDto> Relationships,
    IReadOnlyList<DatabaseEvidenceSnippetDto> Evidence);

public record DatabaseSourceRefDto(
    string File,
    string Symbol);

public record DatabaseEvidenceSnippetDto(
    string File,
    int StartLine,
    int EndLine,
    string Reason);
