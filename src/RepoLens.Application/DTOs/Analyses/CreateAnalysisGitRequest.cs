using System.ComponentModel.DataAnnotations;

namespace RepoLens.Application.DTOs.Analyses;

/// <summary>
/// Request to create analysis from a Git URL (contracts/api.md Section 6.1).
/// </summary>
public record CreateAnalysisGitRequest
{
    [Required]
    public string SourceType { get; init; } = "GitUrl";

    [Required]
    [Url]
    public string SourceUrl { get; init; } = string.Empty;
}
