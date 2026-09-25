using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

public class InsufficientEvidenceResponseTests
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

    private sealed class FakeAiProvider(string answer) : IAiProvider
    {
        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = answer,
                Confidence = AiConfidenceLevel.Medium,
                Evidence = []
            });
        }
    }

    [Fact]
    public void ContainsInsufficientEvidenceAcknowledgment_ReturnsExpectedValues()
    {
        Assert.False(InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment(null));
        Assert.False(InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment("   "));
        Assert.False(InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment("Orders are stored in OrderService.cs."));

        Assert.True(InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment(InsufficientEvidenceResponse.DefaultMessage));
        Assert.True(InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment("There is no relevant evidence for GraphQL in this repo."));
        Assert.True(InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment("No evidence found for this endpoint."));
    }

    [Fact]
    public void RequiresInsufficientEvidenceResponse_OnlyForUnusableEvidenceStatuses()
    {
        AnswerValidationResult ResultWith(AnswerValidationStatus status) => new(
            IsValid: false,
            Status: status,
            ValidatedAnswer: "answer",
            ValidatedEvidence: [],
            ClaimDetails: [],
            UnsupportedClaims: [],
            RejectedCitations: []);

        Assert.False(InsufficientEvidenceResponse.RequiresInsufficientEvidenceResponse(null));
        Assert.False(InsufficientEvidenceResponse.RequiresInsufficientEvidenceResponse(ResultWith(AnswerValidationStatus.FullySupported)));
        Assert.False(InsufficientEvidenceResponse.RequiresInsufficientEvidenceResponse(ResultWith(AnswerValidationStatus.PartiallySupported)));
        Assert.True(InsufficientEvidenceResponse.RequiresInsufficientEvidenceResponse(ResultWith(AnswerValidationStatus.InsufficientEvidence)));
        Assert.True(InsufficientEvidenceResponse.RequiresInsufficientEvidenceResponse(ResultWith(AnswerValidationStatus.Unsupported)));
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenNoChunksAndAiFabricatesFacts_ReturnsCanonicalInsufficientEvidenceResponse()
    {
        var service = new RagService(
            new FakeAiProvider("The repository uses PostgreSQL in `DbContext.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever([]),
            new AiEvidenceValidator());

        var result = await service.AnswerQuestionAsync(Guid.NewGuid(), "What database does this repo use?");

        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.False(result.HasSufficientEvidence);
        Assert.Empty(result.Evidence);
        Assert.Empty(result.RetrievedChunks);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenNoChunksAndNoValidator_ReturnsCanonicalInsufficientEvidenceResponseWithoutHallucination()
    {
        var service = new RagService(
            new FakeAiProvider("The repository exposes GET /api/payments in `PaymentController.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever([]));

        var result = await service.AnswerQuestionAsync(Guid.NewGuid(), "Which payment endpoints exist?");

        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.False(result.HasSufficientEvidence);
        Assert.Empty(result.Evidence);
        Assert.DoesNotContain("PaymentController.cs", result.Answer);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenAllClaimsUnsupported_ReturnsCanonicalInsufficientEvidenceResponse()
    {
        var analysisId = Guid.NewGuid();
        var chunks = new List<VectorChunkSearchResult> { MakeSearchResult(analysisId) };
        var service = new RagService(
            new FakeAiProvider("Payments are handled by `StripeGateway.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever(chunks),
            new AiEvidenceValidator());

        var result = await service.AnswerQuestionAsync(analysisId, "How are payments processed?");

        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.False(result.HasSufficientEvidence);
        Assert.Empty(result.Evidence);
        Assert.DoesNotContain("StripeGateway.cs", result.Answer);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenEvidenceIsSufficient_PreservesGroundedAnswer()
    {
        var analysisId = Guid.NewGuid();
        var chunks = new List<VectorChunkSearchResult> { MakeSearchResult(analysisId) };
        var service = new RagService(
            new FakeAiProvider("Authentication is handled in `AuthService.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever(chunks),
            new AiEvidenceValidator());

        var result = await service.AnswerQuestionAsync(analysisId, "Where is authentication handled?");

        Assert.NotEqual(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.True(result.HasSufficientEvidence);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenInsufficientEvidence_ConfidenceIsLowAndNoClaimLeaks()
    {
        var service = new RagService(
            new FakeAiProvider("Authentication uses JWT in `TokenService.cs`."),
            new FakeEmbeddingProvider(),
            new FakeVectorChunkRetriever([]),
            new AiEvidenceValidator(),
            new AiConfidenceCalculator());

        var result = await service.AnswerQuestionAsync(Guid.NewGuid(), "How does authentication work?");

        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.False(result.HasSufficientEvidence);
        Assert.Equal(AiConfidenceLevel.Low, result.Confidence);
        Assert.DoesNotContain("TokenService.cs", result.Answer);
    }
}
