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

    // Transient in-memory / RAG metadata accessors (T083 / FR-009)
    public int StartLine => Evidence != null ? Evidence.StartLine : _startLine;
    public int EndLine => Evidence != null ? Evidence.EndLine : _endLine;
    public float ConfidenceScore => Evidence?.Confidence != null ? Evidence.Confidence.Value : _confidenceScore;
    public IReadOnlyList<Guid> EvidenceIds => _evidenceIds.Count > 0 ? _evidenceIds : (EvidenceId.HasValue ? [EvidenceId.Value] : []);

    private int _startLine = 1;
    private int _endLine = 1;
    private float _confidenceScore = 1.0f;
    private List<Guid> _evidenceIds = [];

    public void SetLineRange(int startLine, int endLine)
    {
        _startLine = Math.Max(1, startLine);
        _endLine = Math.Max(_startLine, endLine);
    }

    public void SetConfidence(float score) => _confidenceScore = score;

    public void SetEvidenceIds(IEnumerable<Guid> ids) => _evidenceIds = ids.Distinct().ToList();

    public static DocumentChunk Create(
        Guid analysisId,
        string content,
        int tokenCount,
        int chunkIndex,
        Guid? sourceFileId = null,
        Guid? evidenceId = null,
        int startLine = 1,
        int endLine = 1,
        float confidenceScore = 1.0f,
        IEnumerable<Guid>? evidenceIds = null,
        Guid? id = null)
    {
        var chunk = new DocumentChunk
        {
            Id = id ?? Guid.NewGuid(),
            AnalysisId = analysisId,
            SourceFileId = sourceFileId,
            Content = content,
            TokenCount = tokenCount,
            ChunkIndex = chunkIndex,
            EvidenceId = evidenceId
        };
        chunk.SetLineRange(startLine, endLine);
        chunk.SetConfidence(confidenceScore);
        if (evidenceIds != null)
        {
            chunk.SetEvidenceIds(evidenceIds);
        }
        else if (evidenceId.HasValue)
        {
            chunk.SetEvidenceIds([evidenceId.Value]);
        }
        return chunk;
    }

    // TODO: [Giả định cần chốt với nhóm] Thuộc tính Embedding (Vector pgvector) chưa khai báo ở T025,
    // tuân thủ nghiêm ngặt SRS §36 (chỉ bổ sung khi Người 5 triển khai task Vector/RAG).

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public SourceFile? SourceFile { get; set; }

    public Evidence? Evidence { get; set; }

    #endregion
}
