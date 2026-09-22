namespace RepoLens.Application.DTOs.Architecture;

/// <summary>
/// Architecture nodes and relationships response (contracts/api.md Section 10).
/// </summary>
public record ArchitectureResponse(
    Guid AnalysisId,
    IReadOnlyList<ArchitectureNodeDto> Nodes,
    IReadOnlyList<ArchitectureEdgeDto> Edges);

public record ArchitectureNodeDto(
    string Id,
    string Type,
    string Name,
    string Path,
    object? Metadata = null);

public record ArchitectureEdgeDto(
    string Id,
    string Source,
    string Target,
    string Type,
    string Confidence,
    EvidenceSnippetDto? Evidence = null,
    string? EvidenceId = null);

public record EvidenceSnippetDto(
    string File,
    int StartLine,
    int EndLine);
