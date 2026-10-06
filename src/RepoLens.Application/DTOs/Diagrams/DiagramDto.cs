using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Diagrams;

/// <summary>
/// Root DTO for repository diagrams, tailored to each repository type (Giai đoạn 2).
/// Follows Archify visualization principles: bounded node count, role-based styling,
/// evidence traceability, and upstream/downstream reach tracing.
/// </summary>
public sealed class DiagramDto
{
    /// <summary>The specific diagram type (e.g. "architecture", "endpoints", "erd", "route_map", "component_tree", "system_overview", "namespace_class", "call_flow", "unsupported").</summary>
    public string DiagramType { get; init; } = string.Empty;

    /// <summary>Detected repository type.</summary>
    public RepositoryType RepositoryType { get; init; }

    /// <summary>Status of the diagram ("Success", "NotDetected", "Unsupported").</summary>
    public string Status { get; init; } = "Success";

    /// <summary>Descriptive message, especially when status is NotDetected or Unsupported.</summary>
    public string? Message { get; init; }

    /// <summary>Whether a database layer was detected in this repository.</summary>
    public bool DatabaseDetected { get; init; }

    /// <summary>Available diagram types for this repository (primary and secondary).</summary>
    public IReadOnlyList<string> AvailableDiagramTypes { get; init; } = [];

    /// <summary>Primary diagram nodes (max ~30, initial view ~8-12).</summary>
    public IReadOnlyList<DiagramNodeDto> Nodes { get; init; } = [];

    /// <summary>Directed edges between nodes.</summary>
    public IReadOnlyList<DiagramEdgeDto> Edges { get; init; } = [];

    /// <summary>Detail cards for each node, displayed when a user clicks a node.</summary>
    public IReadOnlyList<DiagramDetailCardDto> DetailCards { get; init; } = [];
}

/// <summary>
/// A node in the repository diagram.
/// </summary>
public sealed class DiagramNodeDto
{
    /// <summary>Unique identifier for the node within the diagram.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Node kind: "controller", "service", "repository", "database", "route", "component",
    /// "hook", "external_api", "project", "namespace", "class", "method", "group".
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Role for visual coloring: "Controller", "Service", "Repository", "Database",
    /// "Page", "Component", "Project", "Class", "General", "Group".
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Parent node ID for hierarchical drill-down or grouping.</summary>
    public string? ParentId { get; init; }

    /// <summary>Evidence paths and line ranges supporting the existence of this node.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>Optional child diagram type to open on double-click/drill-down (used in Monorepo system overview).</summary>
    public string? ChildDiagramType { get; init; }

    /// <summary>Additional metadata specific to node kind.</summary>
    public object? Metadata { get; init; }
}

/// <summary>
/// A directed edge between two nodes.
/// </summary>
public sealed class DiagramEdgeDto
{
    /// <summary>Unique edge identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Source node ID.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>Target node ID.</summary>
    public string To { get; init; } = string.Empty;

    /// <summary>Edge kind: "calls", "queries", "renders", "references", "depends_on", "imports", "implements".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Optional display label on the edge.</summary>
    public string? Label { get; init; }

    /// <summary>Confidence level ("High", "Medium", "Low").</summary>
    public string Confidence { get; init; } = "High";

    /// <summary>
    /// Whether this edge is inferred (nét đứt) rather than directly extracted from code (nét liền).
    /// Direct code evidence -> isInferred = false.
    /// Inferred relationship -> isInferred = true.
    /// </summary>
    public bool IsInferred { get; init; }
}

/// <summary>
/// Detail card displayed when a node is clicked.
/// Contains traceability metadata ("SRC n"), role, description, and reach dependencies.
/// </summary>
public sealed class DiagramDetailCardDto
{
    /// <summary>Corresponding node ID.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Card header title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Architectural role.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Relative file path.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Code symbol name (class, method, interface).</summary>
    public string? Symbol { get; init; }

    /// <summary>Source line range label (e.g. "SRC 1 (L12-L45)").</summary>
    public string? LineRange { get; init; }

    /// <summary>Human-readable explanation of this component's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>List of node IDs that depend on this node ("Ai phụ thuộc nó" / Upstream).</summary>
    public IReadOnlyList<string> UpstreamNodes { get; init; } = [];

    /// <summary>List of node IDs that this node depends on ("Nó phụ thuộc vào" / Downstream).</summary>
    public IReadOnlyList<string> DownstreamNodes { get; init; } = [];
}
