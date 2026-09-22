namespace RepoLens.Domain.Enums;

/// <summary>
/// Status of analysis for an individual source file.
/// </summary>
public enum FileAnalysisStatus
{
    Pending = 1,
    Analyzed = 2,
    Skipped = 3,
    Failed = 4
}
