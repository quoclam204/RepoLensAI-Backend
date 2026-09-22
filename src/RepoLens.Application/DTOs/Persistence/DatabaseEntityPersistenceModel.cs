namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a database entity/model (T059).
/// </summary>
public record DatabaseEntityPersistenceModel(
    Guid? Id,
    string Name,
    string EntityType,
    string? SourceSymbolKey,
    string? SourceSymbolFullName,
    Guid? SourceSymbolId);
