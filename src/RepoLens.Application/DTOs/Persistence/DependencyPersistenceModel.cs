using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a dependency edge (T059).
/// </summary>
public record DependencyPersistenceModel(
    Guid? Id,
    string SourceId,
    string TargetId,
    DependencyType DependencyType,
    string? EvidenceKey,
    Guid? EvidenceId);
