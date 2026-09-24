using RepoLens.Application.DTOs.Persistence;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Application abstraction for analyzing a repository and extracting knowledge models.
/// </summary>
public interface IRepositoryAnalyzer
{
    /// <summary>
    /// Analyzes source code of an untrusted repository on disk, producing a normalized,
    /// evidence-grounded AnalysisResultModel ready for persistence and architecture modeling.
    /// </summary>
    /// <param name="repositoryPath">Absolute local directory path where the repository was cloned or extracted.</param>
    /// <param name="analysisId">Unique identifier of the current analysis run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized analysis result model containing projects, files, symbols, relationships, endpoints, and evidences.</returns>
    Task<AnalysisResultModel> AnalyzeAsync(
        string repositoryPath,
        Guid analysisId,
        CancellationToken cancellationToken = default);
}
