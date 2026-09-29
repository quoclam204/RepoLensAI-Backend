using RepoLens.Application.Abstractions;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Work item queued for background analysis processing (T054).
/// </summary>
/// <param name="AnalysisId">The unique identifier of the analysis.</param>
/// <param name="Request">The acquisition request specification.</param>
/// <param name="TempArchiveFilePath">Optional staging path on disk for uploaded archive.</param>
public sealed record AnalysisWorkItem(
    Guid AnalysisId,
    RepositorySourceRequest Request,
    string? TempArchiveFilePath = null);

/// <summary>
/// Queue abstraction for bounded asynchronous analysis orchestration (T054).
/// </summary>
public interface IAnalysisQueue
{
    /// <summary>
    /// Enqueues an analysis job for background execution.
    /// </summary>
    ValueTask EnqueueAsync(AnalysisWorkItem workItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues an analysis job. Waits asynchronously until an item is available.
    /// </summary>
    ValueTask<AnalysisWorkItem> DequeueAsync(CancellationToken cancellationToken = default);
}
