namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a document chunk for RAG/vector indexing (T059).
/// </summary>
public record DocumentChunkPersistenceModel(
    Guid? Id,
    string? FilePath,
    Guid? SourceFileId,
    string Content,
    int TokenCount,
    int ChunkIndex,
    string? EvidenceKey,
    Guid? EvidenceId,
    int StartLine = 1,
    int EndLine = 1,
    float ConfidenceScore = 1.0f,
    IReadOnlyList<Guid>? EvidenceIds = null);
