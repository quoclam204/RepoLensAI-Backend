using System.Text.Json.Serialization;

namespace RepoLens.Application.Models.Archify;

/// <summary>
/// Root Archify document conforming to the Archify C4 schema specification (docs/ideas/archify.md).
/// </summary>
public sealed record ArchifyDocument(
    [property: JsonPropertyName("system")] ArchifySystem System);

/// <summary>
/// C4 System Context in Archify.
/// </summary>
public sealed record ArchifySystem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("containers")] IReadOnlyList<ArchifyContainer> Containers,
    [property: JsonPropertyName("relationships")] IReadOnlyList<ArchifyRelationship> Relationships);

/// <summary>
/// C4 Container (e.g. Deployable service, WebApi, Class library, Frontend app) in Archify.
/// </summary>
public sealed record ArchifyContainer(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("technology")] string Technology,
    [property: JsonPropertyName("components")] IReadOnlyList<ArchifyComponent> Components,
    [property: JsonPropertyName("evidenceIds")] IReadOnlyList<string>? EvidenceIds = null);

/// <summary>
/// C4 Component (e.g. Controller, Service, Repository, DbContext) within a container.
/// </summary>
public sealed record ArchifyComponent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("evidenceIds")] IReadOnlyList<string> EvidenceIds);

/// <summary>
/// C4 Relationship connecting containers or components, with evidence tracing and confidence.
/// </summary>
public sealed record ArchifyRelationship(
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("targetId")] string TargetId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("evidenceIds")] IReadOnlyList<string> EvidenceIds,
    [property: JsonPropertyName("confidence")] string? Confidence = null);
