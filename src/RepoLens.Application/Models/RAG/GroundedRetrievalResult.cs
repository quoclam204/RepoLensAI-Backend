namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Verified retrieval result grounded in static analysis evidence for RAG question answering.
/// </summary>
public sealed record GroundedRetrievalResult(
    string Question,
    IReadOnlyList<RetrievedEvidenceItem> Items,
    bool HasSufficientEvidence,
    string ConfidenceLevel,
    string GroundingStatus);

public sealed record RetrievedEvidenceItem(
    Guid EvidenceId,
    string FilePath,
    int StartLine,
    int EndLine,
    string Snippet,
    string EvidenceType,
    float ConfidenceScore,
    string? Symbol);

/// <summary>
/// Verified document chunk retrieved from static analysis knowledge store for RAG grounding.
/// </summary>
public sealed record RetrievedChunkItem(
    Guid ChunkId,
    Guid AnalysisId,
    Guid? SourceFileId,
    string FilePath,
    string? Symbol,
    int StartLine,
    int EndLine,
    string Content,
    int TokenCount,
    int ChunkIndex,
    Guid? EvidenceId,
    float ConfidenceScore);
