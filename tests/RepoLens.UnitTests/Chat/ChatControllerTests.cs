using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.DTOs.Chat;
using RepoLens.Application.Models.RAG;

namespace RepoLens.UnitTests.Chat;

public class ChatControllerTests
{
    private const int StandardDimension = 1536;

    private static float[] MakeTestEmbedding(float fill = 0.1f)
    {
        var vector = new float[StandardDimension];
        Array.Fill(vector, fill);
        return vector;
    }

    private static VectorChunkSearchResult MakeSearchResult(
        Guid analysisId,
        string filePath = "src/Services/AuthService.cs",
        string? symbol = "AuthService.Authenticate",
        int startLine = 10,
        int endLine = 30)
    {
        return new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: filePath,
            Symbol: symbol,
            StartLine: startLine,
            EndLine: endLine,
            Content: "public bool Authenticate(string user, string pass) => true;",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.15,
            SimilarityScore: 0.85);
    }

    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>([MakeTestEmbedding()]);
        }
    }

    private sealed class FakeVectorChunkRetriever(IReadOnlyList<VectorChunkSearchResult> chunks) : IVectorChunkRetriever
    {
        public Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
            Guid analysisId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(chunks);
        }
    }

    private sealed class FakeAiProvider(string answer, AiConfidenceLevel confidence = AiConfidenceLevel.Medium) : IAiProvider
    {
        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = answer,
                Confidence = confidence,
                Evidence = []
            });
        }
    }

    private static RepoLens.Api.Controllers.ChatController CreateController(Guid analysisId, string aiAnswer, AiConfidenceLevel confidence = AiConfidenceLevel.Medium)
    {
        var chunks = new List<VectorChunkSearchResult> { MakeSearchResult(analysisId) };
        var service = new RepoLens.Application.Services.RagService(
            new FakeAiProvider(aiAnswer, confidence),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever(chunks),
            new RepoLens.Application.Services.AiEvidenceValidator(),
            new RepoLens.Application.Services.AiConfidenceCalculator());
        return new RepoLens.Api.Controllers.ChatController(service);
    }

    [Fact]
    public async Task ChatAsync_WhenQuestionIsGrounded_ReturnsAnswerWithEvidenceAndConfidence()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var controller = CreateController(analysisId, "Authentication is handled in `AuthService.cs`.");
        var request = new ChatRequest { Question = "Where is authentication handled?" };

        // Act
        var actionResult = await controller.ChatAsync(analysisId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.NotEqual(InsufficientEvidenceResponse.DefaultMessage, response.Answer);
        Assert.NotEmpty(response.Evidence);
        Assert.Contains(response.Evidence, e => e.File == "src/Services/AuthService.cs");
        Assert.Contains(response.Confidence, new[] { "high", "medium", "low" });
    }

    [Fact]
    public async Task ChatAsync_WhenQuestionIsEmpty_ReturnsBadRequest()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var controller = CreateController(analysisId, "Some answer.");
        var request = new ChatRequest { Question = "   " };

        // Act
        var actionResult = await controller.ChatAsync(analysisId, request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task ChatAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var controller = CreateController(analysisId, "Some answer.");

        // Act
        var actionResult = await controller.ChatAsync(analysisId, null!, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task ChatAsync_WhenEvidenceIsInsufficient_ReturnsCanonicalInsufficientEvidenceResponse()
    {
        // Arrange: AI fabricates an unsupported claim with no usable evidence.
        var analysisId = Guid.NewGuid();
        var service = new RepoLens.Application.Services.RagService(
            new FakeAiProvider("Payments are handled by `StripeGateway.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever([MakeSearchResult(analysisId)]),
            new RepoLens.Application.Services.AiEvidenceValidator());
        var controller = new RepoLens.Api.Controllers.ChatController(service);
        var request = new ChatRequest { Question = "How are payments processed?" };

        // Act
        var actionResult = await controller.ChatAsync(analysisId, request, CancellationToken.None);

        // Assert: canonical T090 message, empty evidence, no fabricated file leaks.
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, response.Answer);
        Assert.Empty(response.Evidence);
        Assert.DoesNotContain("StripeGateway.cs", response.Answer);
    }

    [Fact]
    public async Task ChatAsync_WhenNoChunksRetrieved_ReturnsCanonicalInsufficientEvidenceResponse()
    {
        // Arrange: empty retrieval forces T090 canonical response.
        var analysisId = Guid.NewGuid();
        var service = new RepoLens.Application.Services.RagService(
            new FakeAiProvider("Everything is in `EverythingService.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever([]),
            new RepoLens.Application.Services.AiEvidenceValidator());
        var controller = new RepoLens.Api.Controllers.ChatController(service);
        var request = new ChatRequest { Question = "What exists in this repo?" };

        // Act
        var actionResult = await controller.ChatAsync(analysisId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, response.Answer);
        Assert.Empty(response.Evidence);
        Assert.DoesNotContain("EverythingService.cs", response.Answer);
    }

    private sealed class StubRagService(RagResult canned) : IRagService
    {
        public Task<RagResult> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(canned);
        }

        public Task<RagResult> AnswerQuestionAsync(Guid analysisId, string question, int topK = 5, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(canned);
        }
    }

    [Theory]
    [InlineData(AiConfidenceLevel.High, "high")]
    [InlineData(AiConfidenceLevel.Medium, "medium")]
    [InlineData(AiConfidenceLevel.Low, "low")]
    [InlineData(AiConfidenceLevel.Unknown, "unknown")]
    public async Task ChatAsync_MapsConfidenceLevels_ToContractStrings(AiConfidenceLevel level, string expected)
    {
        // Arrange: stub IRagService so the controller mapping is tested in isolation,
        // independent of RagService confidence fallback rules.
        var analysisId = Guid.NewGuid();
        var chunk = MakeSearchResult(analysisId);
        var evidence = new List<AiEvidenceItem>
        {
            new() { File = chunk.FilePath, Symbol = chunk.Symbol, StartLine = chunk.StartLine, EndLine = chunk.EndLine, Reason = "test" }
        };
        var canned = new RagResult(
            Question: "Where is authentication handled?",
            Answer: "Authentication is handled in `AuthService.cs`.",
            RetrievedChunks: [chunk],
            Evidence: evidence,
            Confidence: level,
            HasSufficientEvidence: true);
        var controller = new RepoLens.Api.Controllers.ChatController(new StubRagService(canned));
        var request = new ChatRequest { Question = "Where is authentication handled?" };

        // Act
        var actionResult = await controller.ChatAsync(analysisId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.Equal(expected, response.Confidence);
    }

    [Fact]
    public async Task ChatAsync_PropagatesCancellationToken()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var controller = CreateController(analysisId, "Some answer.");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            controller.ChatAsync(analysisId, new ChatRequest { Question = "q" }, cts.Token));
    }
}
