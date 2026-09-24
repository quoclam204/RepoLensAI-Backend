using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents an analyzed source file within a project (T018).
/// </summary>
public class SourceFile
{
    public Guid Id { get; set; }

    // TODO: [Giả định cần chốt với nhóm] tasks.md chỉ nêu liên kết Project,
    // nhưng để tối ưu hóa truy vấn lọc toàn bộ file theo Analysis (GET /api/analyses/{id}/files),
    // lưu trữ cả AnalysisId và ProjectId.
    public Guid AnalysisId { get; set; }

    public Guid ProjectId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Hash { get; set; } = string.Empty;

    public FileAnalysisStatus AnalysisStatus { get; set; } = FileAnalysisStatus.Pending;

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public Project Project { get; set; } = null!;

    public ICollection<CodeSymbol> Symbols { get; set; } = new List<CodeSymbol>();

    public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();

    #endregion
}
