using RepoLens.Domain.Entities;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a segmented chunk of source code or documentation prepared for vector embedding and RAG retrieval (T025).
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public Guid? SourceFileId { get; set; }

    public string Content { get; set; } = string.Empty;

    public int TokenCount { get; set; }

    public int ChunkIndex { get; set; }

    public Guid? EvidenceId { get; set; }

    /// <summary>
    /// Embedding vector for vector search (T084).
    /// Stored as float[] and mapped to PostgreSQL pgvector vector(1536).
    /// </summary>
    public float[]? Embedding { get; set; }

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public SourceFile? SourceFile { get; set; }

    public Evidence? Evidence { get; set; }

    #endregion
}