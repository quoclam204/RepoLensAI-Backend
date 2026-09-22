using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a single execution run of repository analysis (T016).
/// </summary>
public class Analysis
{
    public Guid Id { get; set; }

    public Guid RepositoryId { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.Created;

    public string CurrentStage { get; set; } = string.Empty;

    public string? CommitHash { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Error { get; set; }

    #region Navigation Properties

    public Repository Repository { get; set; } = null!;

    public ICollection<Project> Projects { get; set; } = new List<Project>();

    public ICollection<SourceFile> SourceFiles { get; set; } = new List<SourceFile>();

    public ICollection<Dependency> Dependencies { get; set; } = new List<Dependency>();

    public ICollection<ApiEndpoint> ApiEndpoints { get; set; } = new List<ApiEndpoint>();

    public ICollection<DatabaseEntity> DatabaseEntities { get; set; } = new List<DatabaseEntity>();

    public ICollection<DatabaseRelationship> DatabaseRelationships { get; set; } = new List<DatabaseRelationship>();

    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();

    public ICollection<AnalysisIssue> Issues { get; set; } = new List<AnalysisIssue>();

    public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();

    #endregion
}
