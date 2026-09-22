using Microsoft.Extensions.Logging;
using RepoLens.Application.Abstractions;

namespace RepoLens.Infrastructure.Storage;

/// <summary>
/// Concrete implementation of <see cref="ITemporaryWorkspace"/> (T031).
/// Manages an isolated directory on disk scoped to a single AnalysisId.
/// Handles OS-specific cleanup issues (e.g. read-only file attributes on Windows
/// from Git clone operations).
/// </summary>
public sealed class TemporaryWorkspace : ITemporaryWorkspace
{
    private readonly ILogger<TemporaryWorkspace> _logger;
    private bool _disposed;

    public TemporaryWorkspace(Guid analysisId, string rootPath, ILogger<TemporaryWorkspace> logger)
    {
        AnalysisId = analysisId;
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Guid AnalysisId { get; }

    /// <inheritdoc />
    public string RootPath { get; }

    /// <inheritdoc />
    public bool Exists => Directory.Exists(RootPath);

    /// <inheritdoc />
    public async Task CleanupAsync()
    {
        if (!Directory.Exists(RootPath))
        {
            _logger.LogDebug(
                "Workspace for analysis {AnalysisId} already cleaned up at {Path}",
                AnalysisId, RootPath);
            return;
        }

        try
        {
            _logger.LogInformation(
                "Cleaning up workspace for analysis {AnalysisId} at {Path}",
                AnalysisId, RootPath);

            // Run on thread pool to avoid blocking the caller for large directories
            await Task.Run(() => ForceDeleteDirectory(RootPath));

            _logger.LogInformation(
                "Successfully cleaned up workspace for analysis {AnalysisId}",
                AnalysisId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to clean up workspace for analysis {AnalysisId} at {Path}",
                AnalysisId, RootPath);
            throw;
        }
    }

    /// <summary>
    /// Recursively removes read-only attributes and deletes the directory.
    /// On Windows, files cloned from Git (e.g., .git/objects/pack) often have
    /// FileAttributes.ReadOnly set, which causes Directory.Delete to throw
    /// UnauthorizedAccessException. This method strips those attributes first.
    /// </summary>
    private static void ForceDeleteDirectory(string path)
    {
        // First, recursively clear read-only attributes on all files
        var directoryInfo = new DirectoryInfo(path);

        foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                file.Attributes = FileAttributes.Normal;
            }
        }

        foreach (var dir in directoryInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            if (dir.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                dir.Attributes = FileAttributes.Normal;
            }
        }

        // Now delete the entire directory tree
        Directory.Delete(path, recursive: true);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await CleanupAsync();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            // Synchronous fallback — blocking call
            CleanupAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}
