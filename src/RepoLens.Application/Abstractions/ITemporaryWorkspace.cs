namespace RepoLens.Application.Abstractions;

/// <summary>
/// Represents an isolated temporary workspace for a single analysis execution (T031).
/// Each workspace is keyed by AnalysisId to guarantee data isolation between analyses.
/// Implements IAsyncDisposable/IDisposable for automatic cleanup.
/// </summary>
public interface ITemporaryWorkspace : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The analysis this workspace is associated with.
    /// Used as isolation boundary (NFR-004).
    /// </summary>
    Guid AnalysisId { get; }

    /// <summary>
    /// Absolute path to the workspace root directory on disk.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Whether the workspace directory currently exists on disk.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Removes all files and directories in the workspace.
    /// Handles OS-specific issues such as read-only file attributes on Windows.
    /// Safe to call multiple times.
    /// </summary>
    Task CleanupAsync();
}

/// <summary>
/// Manages creation and lifecycle of temporary workspaces (T031).
/// Infrastructure layer provides the concrete implementation.
/// </summary>
public interface ITemporaryWorkspaceManager
{
    /// <summary>
    /// Creates a new isolated workspace for the given analysis.
    /// The workspace directory is created immediately on disk.
    /// </summary>
    /// <param name="analysisId">The analysis to create a workspace for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A workspace scoped to the analysis.</returns>
    Task<ITemporaryWorkspace> CreateWorkspaceAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing workspace for the given analysis, if it exists.
    /// </summary>
    /// <param name="analysisId">The analysis whose workspace to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workspace if found; null otherwise.</returns>
    Task<ITemporaryWorkspace?> GetWorkspaceAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up and removes the workspace for the given analysis.
    /// Safe to call even if no workspace exists.
    /// </summary>
    /// <param name="analysisId">The analysis whose workspace to clean up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupWorkspaceAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);
}
