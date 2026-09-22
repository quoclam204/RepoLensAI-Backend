using RepoLens.Application.DTOs.Persistence;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Service responsible for persisting the static analysis and knowledge model results into storage (T059).
/// </summary>
public interface IAnalysisPersistenceService
{
    /// <summary>
    /// Persists all analyzed projects, files, symbols, dependencies, endpoints, database entities,
    /// and source evidences atomically within a single database transaction.
    /// </summary>
    /// <param name="result">The complete normalized analysis result from pipeline.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PersistAnalysisResultAsync(AnalysisResultModel result, CancellationToken ct = default);
}
