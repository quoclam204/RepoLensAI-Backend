using RepoLens.Application.Models.Classification;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Detects the type of a repository using a 3-layer evidence approach:
/// Layer 1: File markers (project files, config files at root and subdirectories)
/// Layer 2: Code-level patterns (attributes, base classes, entry points)
/// Layer 3: Component relationships (project references, namespace dependencies)
///
/// Only reads files — never executes repository code.
/// </summary>
public interface IRepositoryTypeDetector
{
    /// <summary>
    /// Detects the repository type by examining the workspace and existing analysis data.
    /// </summary>
    /// <param name="analysisId">The analysis identifier to retrieve already-extracted symbols, endpoints, and dependencies.</param>
    /// <param name="workspaceRoot">Absolute path to the repository workspace root directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Classification result with type, confidence, and supporting evidence.</returns>
    Task<RepositoryClassification> DetectAsync(Guid analysisId, string workspaceRoot, CancellationToken ct = default);
}
