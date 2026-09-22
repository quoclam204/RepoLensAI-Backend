namespace RepoLens.Analysis.Graph;

/// <summary>
/// Thread-safe accumulator and builder for constructing repository knowledge graphs.
/// </summary>
public sealed class KnowledgeGraphBuilder
{
    private readonly Dictionary<string, KnowledgeNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KnowledgeRelationship> _relationships = [];
    private readonly object _lock = new();

    public IReadOnlyCollection<KnowledgeNode> Nodes
    {
        get
        {
            lock (_lock)
            {
                return _nodes.Values.ToList().AsReadOnly();
            }
        }
    }

    public IReadOnlyCollection<KnowledgeRelationship> Relationships
    {
        get
        {
            lock (_lock)
            {
                return _relationships.ToList().AsReadOnly();
            }
        }
    }

    public bool AddNode(KnowledgeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        lock (_lock)
        {
            return _nodes.TryAdd(node.Id, node);
        }
    }

    public void AddOrUpdateNode(KnowledgeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        lock (_lock)
        {
            _nodes[node.Id] = node;
        }
    }

    public void AddRelationship(KnowledgeRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        lock (_lock)
        {
            _relationships.Add(relationship);
        }
    }

    public bool TryGetNode(string id, out KnowledgeNode? node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_lock)
        {
            return _nodes.TryGetValue(id, out node);
        }
    }

    public IReadOnlyList<KnowledgeRelationship> GetOutgoingRelationships(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        lock (_lock)
        {
            return _relationships
                .Where(r => string.Equals(r.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public IReadOnlyList<KnowledgeRelationship> GetIncomingRelationships(string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        lock (_lock)
        {
            return _relationships
                .Where(r => string.Equals(r.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _relationships.Clear();
        }
    }
}
