using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a dependency or relationship edge between projects, files, or symbols (T020).
/// </summary>
public class Dependency
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    // TODO: [Giả định cần chốt với nhóm] SourceId và TargetId lưu string để biểu diễn linh hoạt
    // cho cả Project ID, File Path hoặc Symbol Identifier trong Graph.
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public DependencyType DependencyType { get; set; }

    public Guid? EvidenceId { get; set; }

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public Evidence? Evidence { get; set; }

    #endregion
}
