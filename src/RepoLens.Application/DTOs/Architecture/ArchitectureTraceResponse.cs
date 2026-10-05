namespace RepoLens.Application.DTOs.Architecture;

/// <summary>
/// Response for tracing the shortest dependency/interaction path between two architecture nodes.
/// </summary>
public record ArchitectureTraceResponse(
    Guid AnalysisId,
    string FromNodeId,
    string ToNodeId,
    bool Found,
    IReadOnlyList<ArchitectureNodeDto> PathNodes,
    IReadOnlyList<ArchitectureEdgeDto> PathEdges,
    IReadOnlyList<EvidenceSnippetDto> Evidences
);
