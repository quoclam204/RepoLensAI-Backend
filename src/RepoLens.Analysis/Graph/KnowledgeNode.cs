using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.Graph;

public enum KnowledgeNodeType
{
    Repository,
    Project,
    File,
    Namespace,
    Class,
    Interface,
    Record,
    Struct,
    Method,
    Property,
    Endpoint,
    DatabaseEntity,
    Service
}

public sealed class KnowledgeNode
{
    public string Id { get; init; }
    public string Name { get; init; }
    public KnowledgeNodeType Type { get; init; }
    public string? FilePath { get; init; }
    public SourceLocation? Location { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public KnowledgeNode(
        string id,
        string name,
        KnowledgeNodeType type,
        string? filePath = null,
        SourceLocation? location = null,
        Dictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        Type = type;
        FilePath = filePath;
        Location = location;

        if (properties is not null)
        {
            Properties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static KnowledgeNode Create(
        string id,
        string name,
        KnowledgeNodeType type,
        string? filePath = null,
        SourceLocation? location = null,
        Dictionary<string, string>? properties = null)
    {
        return new KnowledgeNode(id, name, type, filePath, location, properties);
    }
}
