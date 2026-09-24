namespace RepoLens.Domain.Enums;

/// <summary>
/// Overall lifecycle status of a repository analysis execution.
/// </summary>
public enum AnalysisStatus
{
    Created = 1,
    Cloning = 2,
    Scanning = 3,
    Analyzing = 4,
    Indexing = 5,
    Completed = 6,
    Failed = 7
}
