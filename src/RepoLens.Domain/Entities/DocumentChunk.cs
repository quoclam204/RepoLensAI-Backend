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

    // TODO: [Giả định cần chốt với nhóm] Thuộc tính Embedding (Vector pgvector) chưa khai báo ở T025,
    // tuân thủ nghiêm ngặt SRS §36 (chỉ bổ sung khi Người 5 triển khai task Vector/RAG).

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public SourceFile? SourceFile { get; set; }

    public Evidence? Evidence { get; set; }

    #endregion
}
