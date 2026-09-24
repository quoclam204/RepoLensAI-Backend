namespace RepoLens.Domain.Enums;

/// <summary>
/// Type of non-fatal issue or degradation detected during repository analysis.
/// </summary>
public enum IssueType
{
    UnsupportedLanguage = 1,
    ParserFailure = 2,
    InvalidProject = 3,
    UnreadableFile = 4,
    General = 5
}
