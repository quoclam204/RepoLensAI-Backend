namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a document chunk for RAG/vector indexing (T059, T083, T085).
/// Embedding is populated by T085 (ChunkEmbeddingService) for approved/safe content only;
/// null means not embedded (ineligible, provider unavailable, or provider failure).
/// Must remain a multiple of the pgvector column dimension (T084: vector(1536)) when set.
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
    IReadOnlyList<Guid>? EvidenceIds = null,
    float[]? Embedding = null);
