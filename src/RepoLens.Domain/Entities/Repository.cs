using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a software repository ingested for architecture and static analysis (T015).
/// </summary>
public class Repository
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public RepositorySourceType SourceType { get; set; }

    // TODO: [Giả định cần chốt với nhóm] spec.md gọi là SourceUrl, tasks.md gọi là SourceLocation.
    // Dùng SourceLocation để bao quát được cả URL Git và đường dẫn file ZIP lưu trữ.
    public string SourceLocation { get; set; } = string.Empty;

    public RepositoryStatus Status { get; set; } = RepositoryStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    #region Navigation Properties

    public ICollection<Analysis> Analyses { get; set; } = new List<Analysis>();

    #endregion
}
