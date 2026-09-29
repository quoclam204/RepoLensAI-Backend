using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RepoLens.Application.Abstractions;

namespace RepoLens.Infrastructure.Background;

/// <summary>
/// Hosted background service orchestrating repository analyses from a bounded channel queue (T054).
/// Ensures graceful shutdown, proper scoped lifetime management, and isolated workspace cleanup.
/// </summary>
public sealed class AnalysisBackgroundWorker : BackgroundService
{
    private readonly IAnalysisQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalysisBackgroundWorker> _logger;

    public AnalysisBackgroundWorker(
        IAnalysisQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalysisBackgroundWorker> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analysis Background Worker started and listening for jobs.");

        while (!stoppingToken.IsCancellationRequested)
        {
            AnalysisWorkItem workItem;
            try
            {
                workItem = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            _logger.LogInformation("Processing queued analysis job for analysis {AnalysisId}", workItem.AnalysisId);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<IAnalysisPipeline>();

                if (!string.IsNullOrEmpty(workItem.TempArchiveFilePath) && File.Exists(workItem.TempArchiveFilePath))
                {
                    await using var fileStream = new FileStream(
                        workItem.TempArchiveFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        8192,
                        useAsync: true);

                    var requestWithStream = workItem.Request with
                    {
                        ContentStream = fileStream,
                        ContentLength = fileStream.Length
                    };

                    await pipeline.ExecuteAsync(workItem.AnalysisId, requestWithStream, stoppingToken);
                }
                else
                {
                    await pipeline.ExecuteAsync(workItem.AnalysisId, workItem.Request, stoppingToken);
                }

                _logger.LogInformation("Successfully completed queued analysis job for analysis {AnalysisId}", workItem.AnalysisId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Analysis {AnalysisId} aborted due to application shutdown.", workItem.AnalysisId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during pipeline execution for analysis {AnalysisId}", workItem.AnalysisId);
            }
            finally
            {
                if (!string.IsNullOrEmpty(workItem.TempArchiveFilePath) && File.Exists(workItem.TempArchiveFilePath))
                {
                    try
                    {
                        File.Delete(workItem.TempArchiveFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up staged archive file {Path}", workItem.TempArchiveFilePath);
                    }
                }
            }
        }

        _logger.LogInformation("Analysis Background Worker has gracefully stopped.");
    }
}
