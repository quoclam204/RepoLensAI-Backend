using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting an analysis issue (T059).
/// </summary>
public record AnalysisIssuePersistenceModel(
    Guid? Id,
    string? FilePath,
    IssueType IssueType,
    IssueSeverity Severity,
    string Message);
