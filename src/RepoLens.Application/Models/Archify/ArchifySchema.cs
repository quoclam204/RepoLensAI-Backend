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

/// <summary>
/// Root Archify V3 specification document matching official Archify schema.
/// </summary>
public sealed record ArchifyV3Document(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("diagram_type")] string DiagramType,
    [property: JsonPropertyName("meta")] ArchifyV3Meta Meta,
    [property: JsonPropertyName("components")] IReadOnlyList<ArchifyV3Component> Components,
    [property: JsonPropertyName("boundaries")] IReadOnlyList<ArchifyV3Boundary> Boundaries,
    [property: JsonPropertyName("connections")] IReadOnlyList<ArchifyV3Connection> Connections);

public sealed record ArchifyV3Meta(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle = null,
    [property: JsonPropertyName("animation")] string? Animation = "trace",
    [property: JsonPropertyName("quality_profile")] string? QualityProfile = "showcase");

public sealed record ArchifyV3Component(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("sublabel")] string? Sublabel = null,
    [property: JsonPropertyName("tag")] string? Tag = null,
    [property: JsonPropertyName("icon")] string? Icon = null,
    [property: JsonPropertyName("category")] string? Category = null,
    [property: JsonPropertyName("sources")] IReadOnlyList<string>? Sources = null);

public sealed record ArchifyV3Boundary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("wraps")] IReadOnlyList<string> Wraps);

public sealed record ArchifyV3Connection(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("label")] string? Label = null,
    [property: JsonPropertyName("variant")] string? Variant = null,
    [property: JsonPropertyName("evidenceId")] string? EvidenceId = null,
    [property: JsonPropertyName("confidence")] string? Confidence = null);

