using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a non-fatal problem, parser failure, or degraded extraction during analysis (T024).
/// </summary>
public class AnalysisIssue
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public string? FilePath { get; set; }

    public IssueType IssueType { get; set; }

    public IssueSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    #endregion
}
