using System.ComponentModel.DataAnnotations;

namespace RepoLens.Application.DTOs.Chat;

/// <summary>
/// Request body for the Chat API (T091 / FR-009 / contracts/api.md Section 25).
/// </summary>
public sealed record ChatRequest
{
    /// <summary>
    /// The user's natural language question regarding the analyzed repository.
    /// </summary>
    [Required]
    public string Question { get; init; } = string.Empty;
}
