using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a database relationship (T059).
/// </summary>
public record DatabaseRelationshipPersistenceModel(
    Guid? Id,
    string? SourceEntityName,
    Guid? SourceEntityId,
    string? TargetEntityName,
    Guid? TargetEntityId,
    DatabaseRelationshipType RelationshipType,
    string? EvidenceKey,
    Guid? EvidenceId);
