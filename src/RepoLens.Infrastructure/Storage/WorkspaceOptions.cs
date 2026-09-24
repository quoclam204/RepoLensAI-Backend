namespace RepoLens.Infrastructure.Storage;

/// <summary>
/// Configuration options for the temporary workspace system.
/// Bound from appsettings.json section "Workspace".
/// </summary>
public class WorkspaceOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Workspace";

    /// <summary>
    /// Base directory where all analysis workspaces are created.
    /// Each analysis gets a subdirectory named by its AnalysisId.
    /// Defaults to "{TempPath}/repolens-workspaces" if not configured.
    /// </summary>
    public string BaseDirectory { get; set; } = string.Empty;
}
