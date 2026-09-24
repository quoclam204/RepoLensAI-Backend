using RepoLens.Domain.Entities;

namespace RepoLens.Analysis.Graph;

public enum KnowledgeRelationshipType
{
    Contains,
    Defines,
    DependsOn,
    Implements,
    Inherits,
    Calls,
    Exposes,
    MapsTo,
    Reads,
    Writes
}

public sealed class KnowledgeRelationship
{
    public string SourceId { get; init; }
    public string TargetId { get; init; }
    public KnowledgeRelationshipType Type { get; init; }
    public Domain.Entities.Evidence? Evidence { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public KnowledgeRelationship(
        string sourceId,
        string targetId,
        KnowledgeRelationshipType type,
        Domain.Entities.Evidence? evidence = null,
        Dictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        SourceId = sourceId;
        TargetId = targetId;
        Type = type;
        Evidence = evidence;

        if (properties is not null)
        {
            Properties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static KnowledgeRelationship Create(
        string sourceId,
        string targetId,
        KnowledgeRelationshipType type,
        Domain.Entities.Evidence? evidence = null,
        Dictionary<string, string>? properties = null)
    {
        return new KnowledgeRelationship(sourceId, targetId, type, evidence, properties);
    }
}
