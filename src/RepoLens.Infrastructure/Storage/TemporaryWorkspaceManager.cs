using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;

namespace RepoLens.Infrastructure.Storage;

/// <summary>
/// Concrete implementation of <see cref="ITemporaryWorkspaceManager"/> (T031).
/// Manages creation, retrieval, and cleanup of temporary workspaces.
/// Thread-safe via ConcurrentDictionary for active workspace tracking.
/// </summary>
public sealed class TemporaryWorkspaceManager : ITemporaryWorkspaceManager
{
    private readonly string _baseDirectory;
    private readonly ILogger<TemporaryWorkspace> _workspaceLogger;
    private readonly ILogger<TemporaryWorkspaceManager> _logger;
    private readonly ConcurrentDictionary<Guid, ITemporaryWorkspace> _activeWorkspaces = new();

    public TemporaryWorkspaceManager(
        IOptions<WorkspaceOptions> options,
        ILogger<TemporaryWorkspaceManager> logger,
        ILogger<TemporaryWorkspace> workspaceLogger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workspaceLogger = workspaceLogger ?? throw new ArgumentNullException(nameof(workspaceLogger));

        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Use configured base directory, or default to system temp
        _baseDirectory = string.IsNullOrWhiteSpace(opts.BaseDirectory)
            ? Path.Combine(Path.GetTempPath(), "repolens-workspaces")
            : opts.BaseDirectory;

        // Ensure the base directory exists
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
            _logger.LogInformation("Created workspace base directory at {BaseDirectory}", _baseDirectory);
        }
    }

    /// <inheritdoc />
    public Task<ITemporaryWorkspace> CreateWorkspaceAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspacePath = GetWorkspacePath(analysisId);

        // Safety: prevent directory traversal via crafted Guid (defense-in-depth)
        ValidatePathSafety(workspacePath);

        if (Directory.Exists(workspacePath))
        {
            _logger.LogWarning(
                "Workspace directory already exists for analysis {AnalysisId}. Cleaning up before re-creation.",
                analysisId);
            // Clean up stale workspace from a previous failed run
            var staleWorkspace = new TemporaryWorkspace(analysisId, workspacePath, _workspaceLogger);
            staleWorkspace.CleanupAsync().GetAwaiter().GetResult();
        }

        Directory.CreateDirectory(workspacePath);

        var workspace = new TemporaryWorkspace(analysisId, workspacePath, _workspaceLogger);
        _activeWorkspaces[analysisId] = workspace;

        _logger.LogInformation(
            "Created workspace for analysis {AnalysisId} at {Path}",
            analysisId, workspacePath);

        return Task.FromResult<ITemporaryWorkspace>(workspace);
    }

    /// <inheritdoc />
    public Task<ITemporaryWorkspace?> GetWorkspaceAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_activeWorkspaces.TryGetValue(analysisId, out var workspace))
        {
            return Task.FromResult<ITemporaryWorkspace?>(workspace);
        }

        // Check if directory exists on disk (e.g., after app restart)
        var workspacePath = GetWorkspacePath(analysisId);
        if (Directory.Exists(workspacePath))
        {
            workspace = new TemporaryWorkspace(analysisId, workspacePath, _workspaceLogger);
            _activeWorkspaces[analysisId] = workspace;
            return Task.FromResult<ITemporaryWorkspace?>(workspace);
        }

        return Task.FromResult<ITemporaryWorkspace?>(null);
    }

    /// <inheritdoc />
    public async Task CleanupWorkspaceAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_activeWorkspaces.TryRemove(analysisId, out var workspace))
        {
            await workspace.CleanupAsync();
        }
        else
        {
            // Directory might exist on disk without being tracked
            var workspacePath = GetWorkspacePath(analysisId);
            if (Directory.Exists(workspacePath))
            {
                var orphanedWorkspace = new TemporaryWorkspace(analysisId, workspacePath, _workspaceLogger);
                await orphanedWorkspace.CleanupAsync();
            }
        }

        _logger.LogInformation("Workspace for analysis {AnalysisId} cleaned up", analysisId);
    }

    /// <summary>
    /// Computes the absolute path for a workspace given an AnalysisId.
    /// </summary>
    private string GetWorkspacePath(Guid analysisId)
        => Path.Combine(_baseDirectory, analysisId.ToString("N"));

    /// <summary>
    /// Defense-in-depth: validate the resolved path is actually inside the base directory.
    /// Prevents directory traversal attacks (though Guid.ToString("N") is safe by design).
    /// </summary>
    private void ValidatePathSafety(string resolvedPath)
    {
        var fullResolvedPath = Path.GetFullPath(resolvedPath);
        var fullBasePath = Path.GetFullPath(_baseDirectory);

        if (!fullResolvedPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workspace path '{resolvedPath}' escapes the base directory '{_baseDirectory}'. " +
                "This is a potential directory traversal attack.");
        }
    }
}
