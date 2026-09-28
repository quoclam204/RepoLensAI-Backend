using RepoLens.Application.Models.Pipeline;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Pipeline orchestrator interface for managing analysis execution lifecycle (T052 - T054).
/// Orchestrates repository acquisition, security validation, scanning, and lifecycle state transitions.
/// </summary>
public interface IAnalysisPipeline
{
    /// <summary>
    /// Executes the acquisition and scanning pipeline for the given analysis.
    /// </summary>
    /// <param name="analysisId">The analysis identifier.</param>
    /// <param name="request">The repository acquisition request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pipeline result containing status, stage, and scan results.</returns>
    Task<AnalysisPipelineResult> ExecuteAsync(
        Guid analysisId,
        RepositorySourceRequest request,
        CancellationToken cancellationToken = default);
}
