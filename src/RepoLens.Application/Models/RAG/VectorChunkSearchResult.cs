namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Result of a vector similarity retrieval on document chunks (T086).
/// Exposes chunk content, provenance (file path, line range, evidence), and similarity metrics for RAG grounding.
/// </summary>
public sealed record VectorChunkSearchResult(
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
    float ConfidenceScore,
    double CosineDistance,
    double SimilarityScore);
