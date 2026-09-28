using RepoLens.Application.Models.Scanning;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Abstraction for repository file scanning and metadata extraction (T032 - T036).
/// Recursively scans an isolated repository workspace, applies ignore rules,
/// filters sensitive secrets, detects programming languages and project structures.
/// Produces a <see cref="ScanResult"/> that serves as input for downstream static analysis.
/// </summary>
public interface IScannerService
{
    /// <summary>
    /// Scans the repository in the specified workspace root.
    /// </summary>
    /// <param name="analysisId">The analysis identifier.</param>
    /// <param name="workspaceRoot">Absolute path to the workspace root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated scan result containing discovered files, detected languages, and projects.</returns>
    Task<ScanResult> ScanAsync(Guid analysisId, string workspaceRoot, CancellationToken cancellationToken = default);
}
