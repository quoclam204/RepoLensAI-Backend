namespace RepoLens.Domain.Enums;

/// <summary>
/// Type of relationship between detected database entities.
/// </summary>
public enum DatabaseRelationshipType
{
    OneToOne = 1,
    OneToMany = 2,
    ManyToMany = 3
}
