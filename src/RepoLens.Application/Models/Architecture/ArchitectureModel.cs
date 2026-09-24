namespace RepoLens.Application.Models.Architecture;

/// <summary>
/// Intermediate, visualization-neutral architecture model representing the C4 structural hierarchy
/// (System Context, Containers, Components, Code Elements) backed by source code evidence.
/// </summary>
public sealed record ArchitectureModel(
    string SystemName,
    string Description,
    IReadOnlyList<ArchitectureNode> Nodes,
    IReadOnlyList<ArchitectureRelationship> Relationships,
    IReadOnlyList<ArchitectureEvidence> Evidences);

/// <summary>
/// Architectural node representing an architectural entity (Project/Container, Namespace/Component, Class/Service, Database).
/// </summary>
public sealed record ArchitectureNode(
    string Id,
    string Name,
    ArchitectureNodeType NodeType,
    string? Path,
    string? Technology,
    string? ParentId,
    IReadOnlyDictionary<string, string> Properties);

public enum ArchitectureNodeType
{
    System = 1,
    Container = 2,
    Component = 3,
    CodeElement = 4,
    Database = 5,
    Endpoint = 6,
    ExternalDependency = 7
}

/// <summary>
/// Architectural directed relationship backed by verified source code evidence.
/// </summary>
public sealed record ArchitectureRelationship(
    string SourceId,
    string TargetId,
    string RelationshipType,
    string Confidence,
    IReadOnlyList<string> EvidenceIds);

/// <summary>
/// Verified source code evidence grounding an architectural node or relationship.
/// </summary>
public sealed record ArchitectureEvidence(
    string Id,
    string FilePath,
    int StartLine,
    int EndLine,
    string Snippet,
    string Confidence);
