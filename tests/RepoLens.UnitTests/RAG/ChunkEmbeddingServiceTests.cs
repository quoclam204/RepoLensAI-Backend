using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.DTOs.Persistence;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

public class ChunkEmbeddingServiceTests
{
    private const int Dimension = 1536;

    private static DocumentChunkPersistenceModel MakeChunk(string content, int index = 0)
    {
        return new DocumentChunkPersistenceModel(
            Id: Guid.NewGuid(),
            FilePath: "src/Service.cs",
            SourceFileId: null,
            Content: content,
            TokenCount: 10,
            ChunkIndex: index,
            EvidenceKey: null,
            EvidenceId: null);
    }

    private static AnalysisResultModel MakeResult(params DocumentChunkPersistenceModel[] chunks)
    {
        return new AnalysisResultModel
        {
            AnalysisId = Guid.NewGuid(),
            DocumentChunks = chunks.ToList().AsReadOnly()
        };
    }

    private static float[] MakeVector(float fill = 0.1f)
    {
        var vector = new float[Dimension];
        Array.Fill(vector, fill);
        return vector;
    }

    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        private readonly Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<float[]>>> _handler;

        public int CallCount { get; private set; }
        public List<IReadOnlyList<string>> ReceivedInputs { get; } = [];
        public CancellationToken LastToken { get; private set; }

        public FakeEmbeddingProvider(Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<float[]>>> handler)
        {
            _handler = handler;
        }

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedInputs.Add(inputs);
            LastToken = cancellationToken;
            return await _handler(inputs, cancellationToken).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task EligibleChunk_ReceivesEmbedding_AllSucceeded()
    {
        // Arrange
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromResult<IReadOnlyList<float[]>>([MakeVector(0.5f)]));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(MakeChunk("public class Service {}"));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.AllSucceeded, outcome.Status);
        Assert.Equal(1, outcome.EligibleCount);
        Assert.Equal(1, outcome.EmbeddedCount);
        Assert.Empty(outcome.Errors);
        Assert.Equal(1, provider.CallCount);
        Assert.NotNull(output.DocumentChunks[0].Embedding);
        Assert.Equal(Dimension, output.DocumentChunks[0].Embedding!.Length);
        Assert.Null(input.DocumentChunks[0].Embedding);
    }

    [Fact]
    public async Task MultipleChunks_PreserveOrder_AllSucceeded()
    {
        // Arrange
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select((_, i) => MakeVector(i + 1)).ToList()));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(
            MakeChunk("chunk-a", 0),
            MakeChunk("chunk-b", 1),
            MakeChunk("chunk-c", 2));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.AllSucceeded, outcome.Status);
        Assert.Equal(3, outcome.EmbeddedCount);
        Assert.Equal(1.0f, output.DocumentChunks[0].Embedding![0]);
        Assert.Equal(2.0f, output.DocumentChunks[1].Embedding![0]);
        Assert.Equal(3.0f, output.DocumentChunks[2].Embedding![0]);
        Assert.Equal(["chunk-a", "chunk-b", "chunk-c"], provider.ReceivedInputs[0]);
    }

    [Fact]
    public async Task Batching_PreservesOrder_AllSucceeded()
    {
        // Arrange
        var seenBatches = new List<IReadOnlyList<string>>();
        var provider = new FakeEmbeddingProvider((inputs, _) =>
        {
            seenBatches.Add(inputs.ToList());
            return Task.FromResult<IReadOnlyList<float[]>>(
                inputs.Select(t => MakeVector(t.Length % 7 + 1)).ToList());
        });
        var service = new ChunkEmbeddingService(provider, new ChunkEmbeddingOptions { MaxBatchSize = 2 });
        var input = MakeResult(
            MakeChunk("aaa", 0),
            MakeChunk("bb", 1),
            MakeChunk("ccccc", 2),
            MakeChunk("d", 3),
            MakeChunk("eeee", 4));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.AllSucceeded, outcome.Status);
        Assert.Equal(5, outcome.EmbeddedCount);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal(2, seenBatches[0].Count);
        Assert.Equal(2, seenBatches[1].Count);
        Assert.Single(seenBatches[2]);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal((float)(input.DocumentChunks[i].Content.Length % 7 + 1), output.DocumentChunks[i].Embedding![0]);
        }
    }

    [Fact]
    public async Task ProviderReturnsWrongCount_CompleteFailure_NoVectorsPersisted()
    {
        // Arrange
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromResult<IReadOnlyList<float[]>>([MakeVector()]));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(MakeChunk("a", 0), MakeChunk("b", 1));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.CompleteFailure, outcome.Status);
        Assert.Equal(2, outcome.EligibleCount);
        Assert.Equal(0, outcome.EmbeddedCount);
        Assert.NotEmpty(outcome.Errors);
        Assert.Null(output.DocumentChunks[0].Embedding);
        Assert.Null(output.DocumentChunks[1].Embedding);
    }

    [Fact]
    public async Task ProviderReturnsWrongDimension_CompleteFailure_NoVectorsPersisted()
    {
        // Arrange
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new float[128]).ToList()));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(MakeChunk("content", 0));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.CompleteFailure, outcome.Status);
        Assert.Equal(0, outcome.EmbeddedCount);
        Assert.NotEmpty(outcome.Errors);
        Assert.Null(output.DocumentChunks[0].Embedding);
    }

    [Fact]
    public async Task ProviderThrows_CompleteFailure_ChunksLeftNull()
    {
        // Arrange
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromException<IReadOnlyList<float[]>>(new InvalidOperationException("provider down")));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(MakeChunk("content", 0), MakeChunk("more", 1));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.CompleteFailure, outcome.Status);
        Assert.Equal(2, outcome.EligibleCount);
        Assert.Equal(0, outcome.EmbeddedCount);
        Assert.NotEmpty(outcome.Errors);
        Assert.Null(output.DocumentChunks[0].Embedding);
        Assert.Null(output.DocumentChunks[1].Embedding);
    }

    [Fact]
    public async Task PartialFailure_OneBatchFails_ReturnsPartialFailure()
    {
        // Arrange
        var call = 0;
        var provider = new FakeEmbeddingProvider((inputs, _) =>
        {
            call++;
            if (call == 1)
            {
                return Task.FromException<IReadOnlyList<float[]>>(new InvalidOperationException("batch 1 down"));
            }

            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => MakeVector(0.9f)).ToList());
        });
        var service = new ChunkEmbeddingService(provider, new ChunkEmbeddingOptions { MaxBatchSize = 1 });
        var input = MakeResult(MakeChunk("first", 0), MakeChunk("second", 1));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.PartialFailure, outcome.Status);
        Assert.Equal(2, outcome.EligibleCount);
        Assert.Equal(1, outcome.EmbeddedCount);
        Assert.NotEmpty(outcome.Errors);
        Assert.Null(output.DocumentChunks[0].Embedding);
        Assert.NotNull(output.DocumentChunks[1].Embedding);
    }

    [Fact]
    public async Task NoEligibleChunks_ProviderNotCalled()
    {
        // Arrange
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => MakeVector()).ToList()));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(
            MakeChunk(string.Empty, 0),
            MakeChunk("   ", 1));

        // Act
        var (output, outcome) = await service.PopulateEmbeddingsAsync(input);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.NoEligibleChunks, outcome.Status);
        Assert.Equal(0, outcome.EligibleCount);
        Assert.Equal(0, outcome.EmbeddedCount);
        Assert.Equal(0, provider.CallCount);
        Assert.Same(input, output);
    }

    [Fact]
    public async Task CancellationToken_IsPropagated()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var provider = new FakeEmbeddingProvider((inputs, ct) =>
        {
            Assert.Equal(cts.Token, ct);
            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => MakeVector()).ToList());
        });
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(MakeChunk("content", 0));

        // Act
        var (_, outcome) = await service.PopulateEmbeddingsAsync(input, cts.Token);

        // Assert
        Assert.Equal(ChunkEmbeddingStatus.AllSucceeded, outcome.Status);
        Assert.Equal(cts.Token, provider.LastToken);
    }

    [Fact]
    public async Task CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new FakeEmbeddingProvider((inputs, _) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => MakeVector()).ToList()));
        var service = new ChunkEmbeddingService(provider);
        var input = MakeResult(MakeChunk("content", 0));

        // Act + Assert: cancellation must propagate, not convert to a failure result.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PopulateEmbeddingsAsync(input, cts.Token));
    }
}
