using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Models.Pipeline;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Background;

namespace RepoLens.UnitTests.Background;

public class BackgroundWorkerTests
{
    [Fact]
    public async Task ChannelAnalysisQueue_EnqueueAndDequeue_PreservesWorkItem()
    {
        // Arrange
        var queue = new ChannelAnalysisQueue(capacity: 10);
        var analysisId = Guid.NewGuid();
        var request = new RepositorySourceRequest(RepositorySourceType.GitUrl, Url: "https://github.com/org/repo");
        var workItem = new AnalysisWorkItem(analysisId, request, TempArchiveFilePath: "temp/path.zip");

        // Act
        await queue.EnqueueAsync(workItem);
        var dequeued = await queue.DequeueAsync();

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal(analysisId, dequeued.AnalysisId);
        Assert.Equal("https://github.com/org/repo", dequeued.Request.Url);
        Assert.Equal("temp/path.zip", dequeued.TempArchiveFilePath);
    }

    [Fact]
    public async Task AnalysisBackgroundWorker_ProcessesEnqueuedJobAndDeletesStagedArchive()
    {
        // Arrange
        var queue = new ChannelAnalysisQueue(capacity: 5);
        var fakePipeline = new FakeAnalysisPipeline();

        var analysisId = Guid.NewGuid();
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "dummy archive bytes");

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IAnalysisPipeline>(_ => fakePipeline);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var worker = new AnalysisBackgroundWorker(
            queue,
            scopeFactory,
            NullLogger<AnalysisBackgroundWorker>.Instance);

        var request = new RepositorySourceRequest(RepositorySourceType.ZipUpload, FileName: "test.zip");
        await queue.EnqueueAsync(new AnalysisWorkItem(analysisId, request, TempArchiveFilePath: tempFile));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act: Start worker in background and cancel shortly after execution
        var workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        try { await worker.StopAsync(CancellationToken.None); } catch { }

        // Assert
        Assert.Equal(1, fakePipeline.ExecutionCount);
        Assert.Equal(analysisId, fakePipeline.LastExecutedAnalysisId);

        // Staged archive must be cleaned up
        Assert.False(File.Exists(tempFile));
    }

    private sealed class FakeAnalysisPipeline : IAnalysisPipeline
    {
        public int ExecutionCount { get; private set; }
        public Guid LastExecutedAnalysisId { get; private set; }

        public Task<AnalysisPipelineResult> ExecuteAsync(
            Guid analysisId,
            RepositorySourceRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            LastExecutedAnalysisId = analysisId;
            return Task.FromResult(AnalysisPipelineResult.Succeeded(AnalysisStage.Completed, null!));
        }
    }
}
