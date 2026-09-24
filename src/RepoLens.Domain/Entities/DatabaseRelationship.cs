using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a relationship (one-to-one, one-to-many, many-to-many, FK) between database entities (T022).
/// </summary>
public class DatabaseRelationship
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public Guid SourceEntityId { get; set; }

    public Guid TargetEntityId { get; set; }

    public DatabaseRelationshipType RelationshipType { get; set; }

    public Guid? EvidenceId { get; set; }

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public DatabaseEntity SourceEntity { get; set; } = null!;

    public DatabaseEntity TargetEntity { get; set; } = null!;

    public Evidence? Evidence { get; set; }

    #endregion
}
