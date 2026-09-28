using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

public class RagServiceTests
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
        int endLine = 30,
        string content = "public bool Authenticate(string user, string pass) => true;",
        float confidence = 0.9f,
        double similarity = 0.85)
    {
        return new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: filePath,
            Symbol: symbol,
            StartLine: startLine,
            EndLine: endLine,
            Content: content,
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: confidence,
            CosineDistance: 1.0 - similarity,
            SimilarityScore: similarity);
    }

    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<float[]>>> Handler { get; set; }
        public List<IReadOnlyList<string>> CapturedInputs { get; } = [];
        public CancellationToken LastToken { get; private set; }

        public FakeEmbeddingProvider(float[]? returnedVector = null)
        {
            var vector = returnedVector ?? MakeTestEmbedding();
            Handler = (inputs, token) => Task.FromResult<IReadOnlyList<float[]>>([vector]);
        }

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            CapturedInputs.Add(inputs);
            LastToken = cancellationToken;
            return await Handler(inputs, cancellationToken);
        }
    }

    private sealed class FakeVectorChunkRetriever : IVectorChunkRetriever
    {
        public Func<Guid, float[], int, CancellationToken, Task<IReadOnlyList<VectorChunkSearchResult>>> Handler { get; set; }
        public Guid LastAnalysisId { get; private set; }
        public float[]? LastQueryVector { get; private set; }
        public int LastTopK { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public FakeVectorChunkRetriever(IReadOnlyList<VectorChunkSearchResult>? returnedChunks = null)
        {
            var chunks = returnedChunks ?? [];
            Handler = (id, vec, k, token) => Task.FromResult(chunks);
        }

        public async Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
            Guid analysisId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            LastAnalysisId = analysisId;
            LastQueryVector = queryEmbedding;
            LastTopK = topK;
            LastToken = cancellationToken;
            return await Handler(analysisId, queryEmbedding, topK, cancellationToken);
        }
    }

    private sealed class FakeAiProvider : IAiProvider
    {
        public Func<AiRequest, CancellationToken, Task<AiResponse>> Handler { get; set; }
        public List<AiRequest> CapturedRequests { get; } = [];
        public CancellationToken LastToken { get; private set; }

        public FakeAiProvider(string answer = "This is a verified answer based on evidence.", AiConfidenceLevel confidence = AiConfidenceLevel.High)
        {
            Handler = (req, token) => Task.FromResult(new AiResponse
            {
                Answer = answer,
                Confidence = confidence,
                Evidence = []
            });
        }

        public async Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            CapturedRequests.Add(request);
            LastToken = cancellationToken;
            return await Handler(request, cancellationToken);
        }
    }

    [Fact]
    public async Task AnswerQuestionAsync_ConvertsQuestionToQueryEmbedding_AndCallsRetrieverWithAnalysisIdAndTopK()
    {
        // 1 & 5: Question converted into query embedding; retriever called with correct analysis ID & topK
        var analysisId = Guid.NewGuid();
        var question = "Where is authentication handled in this repository?";
        var queryVector = MakeTestEmbedding(0.5f);

        var embeddingProvider = new FakeEmbeddingProvider(queryVector);
        var retriever = new FakeVectorChunkRetriever();
        var aiProvider = new FakeAiProvider();

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        var request = new RagRequest(analysisId, question, topK: 7);
        await service.AnswerQuestionAsync(request);

        // Assert embedding provider received the question
        Assert.Single(embeddingProvider.CapturedInputs);
        Assert.Equal(question, embeddingProvider.CapturedInputs[0][0]);

        // Assert retriever received the analysisId, query vector, and topK
        Assert.Equal(analysisId, retriever.LastAnalysisId);
        Assert.Equal(queryVector, retriever.LastQueryVector);
        Assert.Equal(7, retriever.LastTopK);
    }

    [Fact]
    public async Task AnswerQuestionAsync_PassesRetrievedChunksIntoAiContext()
    {
        // 2 & 3: Retrieved chunks passed into context; AI provider receives expected evidence-grounded request
        var analysisId = Guid.NewGuid();
        var chunk = MakeSearchResult(analysisId, "src/Auth/TokenService.cs", "TokenService.ValidateToken", 15, 30, "public bool ValidateToken(string t) => true;");

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([chunk]);
        var aiProvider = new FakeAiProvider();

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        await service.AnswerQuestionAsync(analysisId, "How are tokens validated?");

        Assert.Single(aiProvider.CapturedRequests);
        var aiRequest = aiProvider.CapturedRequests[0];

        // Check separation of system prompt, context, and prompt
        Assert.Equal("How are tokens validated?", aiRequest.Prompt);
        Assert.NotNull(aiRequest.SystemPrompt);
        Assert.Contains("sole source of truth", aiRequest.SystemPrompt);
        Assert.NotNull(aiRequest.Context);
        Assert.Contains("src/Auth/TokenService.cs", aiRequest.Context);
        Assert.Contains("TokenService.ValidateToken", aiRequest.Context);
        Assert.Contains("public bool ValidateToken(string t) => true;", aiRequest.Context);
    }

    [Fact]
    public async Task AnswerQuestionAsync_PreservesEvidenceTraceabilityInResult()
    {
        // 4: Generated response preserves evidence traceability
        var analysisId = Guid.NewGuid();
        var chunk1 = MakeSearchResult(analysisId, "src/A.cs", "ClassA", 10, 20, "code A", 0.9f, 0.88);
        var chunk2 = MakeSearchResult(analysisId, "src/B.cs", "ClassB", 30, 45, "code B", 0.85f, 0.75);

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([chunk1, chunk2]);
        var aiProvider = new FakeAiProvider("ClassA calls ClassB to perform the operation.");

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        var result = await service.AnswerQuestionAsync(analysisId, "How does A interact with B?");

        Assert.Equal("ClassA calls ClassB to perform the operation.", result.Answer);
        Assert.True(result.HasSufficientEvidence);
        Assert.Equal(2, result.RetrievedChunks.Count);
        Assert.Equal(2, result.Evidence.Count);

        Assert.Equal("src/A.cs", result.Evidence[0].File);
        Assert.Equal("ClassA", result.Evidence[0].Symbol);
        Assert.Equal(10, result.Evidence[0].StartLine);
        Assert.Equal(20, result.Evidence[0].EndLine);

        Assert.Equal("src/B.cs", result.Evidence[1].File);
        Assert.Equal("ClassB", result.Evidence[1].Symbol);
        Assert.Equal(30, result.Evidence[1].StartLine);
        Assert.Equal(45, result.Evidence[1].EndLine);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenNoChunksRetrieved_HandlesGracefullyWithoutFabrication()
    {
        // 6: Empty/no retrieved evidence handled safely without hallucinating
        var analysisId = Guid.NewGuid();
        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([]); // 0 chunks
        var aiProvider = new FakeAiProvider("I cannot find any evidence about GraphQL in this repository.");

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        var result = await service.AnswerQuestionAsync(analysisId, "Does this repo use GraphQL?");

        Assert.False(result.HasSufficientEvidence);
        Assert.Empty(result.RetrievedChunks);
        Assert.Empty(result.Evidence);
        Assert.NotNull(result.ContextPrompt);
        Assert.Contains("No relevant repository evidence chunks were found", result.ContextPrompt);
    }

    [Fact]
    public async Task AnswerQuestionAsync_PropagatesCancellationToken()
    {
        // 7: Cancellation is propagated
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever();
        var aiProvider = new FakeAiProvider();

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        await service.AnswerQuestionAsync(new RagRequest(Guid.NewGuid(), "test"), token);

        Assert.Equal(token, embeddingProvider.LastToken);
        Assert.Equal(token, retriever.LastToken);
        Assert.Equal(token, aiProvider.LastToken);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever();
        var aiProvider = new FakeAiProvider();

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AnswerQuestionAsync(new RagRequest(Guid.NewGuid(), "test"), cts.Token));
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenEmbeddingProviderFails_ThrowsWithoutFabricatingAnswer()
    {
        // 8: Failure in embedding provider propagates; no answer is fabricated
        var embeddingProvider = new FakeEmbeddingProvider
        {
            Handler = (_, _) => throw new HttpRequestException("Embedding API connection refused")
        };
        var retriever = new FakeVectorChunkRetriever();
        var aiProvider = new FakeAiProvider();

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AnswerQuestionAsync(Guid.NewGuid(), "Where is the API?"));

        Assert.Empty(aiProvider.CapturedRequests);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenRetrieverFails_ThrowsWithoutFabricatingAnswer()
    {
        // 8: Failure in retriever propagates; no answer is fabricated
        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever
        {
            Handler = (_, _, _, _) => throw new InvalidOperationException("Database unreachable")
        };
        var aiProvider = new FakeAiProvider();

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnswerQuestionAsync(Guid.NewGuid(), "Where is the API?"));

        Assert.Empty(aiProvider.CapturedRequests);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenAiProviderFails_ThrowsWithoutFabricatingAnswer()
    {
        // 8: Failure in AI provider propagates; no answer is fabricated
        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([MakeSearchResult(Guid.NewGuid())]);
        var aiProvider = new FakeAiProvider
        {
            Handler = (_, _) => throw new TimeoutException("LLM request timed out")
        };

        var service = new RagService(aiProvider, embeddingProvider, retriever);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            service.AnswerQuestionAsync(Guid.NewGuid(), "Where is the API?"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnswerQuestionAsync_WhenQuestionIsWhitespace_ThrowsArgumentException(string invalidQuestion)
    {
        var service = new RagService(new FakeAiProvider(), new FakeEmbeddingProvider(), new FakeVectorChunkRetriever());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnswerQuestionAsync(Guid.NewGuid(), invalidQuestion));
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenAnalysisIdIsEmpty_ThrowsArgumentException()
    {
        var service = new RagService(new FakeAiProvider(), new FakeEmbeddingProvider(), new FakeVectorChunkRetriever());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnswerQuestionAsync(Guid.Empty, "Valid question?"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task AnswerQuestionAsync_WhenTopKIsOutOfRange_ThrowsArgumentOutOfRangeException(int invalidTopK)
    {
        var service = new RagService(new FakeAiProvider(), new FakeEmbeddingProvider(), new FakeVectorChunkRetriever());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AnswerQuestionAsync(Guid.NewGuid(), "Valid question?", invalidTopK));
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenEmbeddingDimensionMismatch_ThrowsInvalidOperationException()
    {
        var invalidDimensionVector = new float[512]; // expected 1536
        var embeddingProvider = new FakeEmbeddingProvider(invalidDimensionVector);

        var service = new RagService(new FakeAiProvider(), embeddingProvider, new FakeVectorChunkRetriever());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnswerQuestionAsync(Guid.NewGuid(), "Valid question?"));

        Assert.Contains("dimension mismatch", ex.Message);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenValidatorProvided_UsesValidatedAnswerAndEvidence()
    {
        var analysisId = Guid.NewGuid();
        var chunk = MakeSearchResult(analysisId, "src/Services/OrderService.cs", "OrderService", 10, 20, "public class OrderService {}");

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([chunk]);
        var aiProvider = new FakeAiProvider("The service is defined in `OrderService.cs`.");
        var validator = new AiEvidenceValidator();

        var service = new RagService(aiProvider, embeddingProvider, retriever, validator);

        var result = await service.AnswerQuestionAsync(analysisId, "Where is the order service?");

        Assert.NotNull(result.Validation);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(AnswerValidationStatus.FullySupported, result.Validation.Status);
        Assert.Equal("The service is defined in `OrderService.cs`.", result.Answer);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenValidatorDetectsUnsupportedClaims_MarksValidationResult()
    {
        var analysisId = Guid.NewGuid();
        var chunk = MakeSearchResult(analysisId, "src/Services/OrderService.cs", "OrderService", 10, 20, "public class OrderService {}");

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([chunk]);
        var aiProvider = new FakeAiProvider("Order service is in `OrderService.cs`. Secret vault is in `src/Auth/SecretVault.cs`.");
        var validator = new AiEvidenceValidator();

        var service = new RagService(aiProvider, embeddingProvider, retriever, validator);

        var result = await service.AnswerQuestionAsync(analysisId, "Where are the services?");

        Assert.NotNull(result.Validation);
        Assert.False(result.Validation.IsValid);
        Assert.Equal(AnswerValidationStatus.PartiallySupported, result.Validation.Status);
        Assert.Contains("[Uncertain - unsupported by retrieved evidence]", result.Answer);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenConfidenceCalculatorProvided_PopulatesConfidenceDetailsAndConfidenceLevel()
    {
        var analysisId = Guid.NewGuid();
        var chunks = new List<VectorChunkSearchResult>
        {
            MakeSearchResult(analysisId, "src/Services/OrderService.cs", "OrderService", 10, 20, "public class OrderService {}", confidence: 0.95f, similarity: 0.90),
            MakeSearchResult(analysisId, "src/Services/IOrderService.cs", "IOrderService", 1, 10, "public interface IOrderService {}", confidence: 0.95f, similarity: 0.88),
            MakeSearchResult(analysisId, "src/Controllers/OrderController.cs", "OrderController", 1, 30, "public class OrderController {}", confidence: 0.90f, similarity: 0.89)
        };

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever(chunks);
        var aiProvider = new FakeAiProvider("The order service is in `OrderService.cs`.");
        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var service = new RagService(aiProvider, embeddingProvider, retriever, validator, calculator);

        var result = await service.AnswerQuestionAsync(analysisId, "Where is the order service?");

        Assert.NotNull(result.ConfidenceDetails);
        Assert.Equal(AiConfidenceLevel.High, result.Confidence);
        Assert.Equal(AiConfidenceLevel.High, result.ConfidenceDetails.Level);
        Assert.True(result.ConfidenceDetails.Score >= 0.75f);
        Assert.Contains("High confidence", result.ConfidenceDetails.Rationale);
        Assert.NotNull(result.ConfidenceDetails.Factors);
        Assert.True(result.ConfidenceDetails.Factors.GroundingFactor > 0.9f);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenConfidenceCalculatorProvidedAndClaimsUnsupported_CapsConfidenceAtLow()
    {
        var analysisId = Guid.NewGuid();
        var chunk = MakeSearchResult(analysisId, "src/Services/OrderService.cs", "OrderService", 10, 20, "public class OrderService {}", confidence: 0.99f, similarity: 0.99);

        var embeddingProvider = new FakeEmbeddingProvider();
        var retriever = new FakeVectorChunkRetriever([chunk]);
        var aiProvider = new FakeAiProvider("Completely hallucinated service in `NonExistentService.cs`.");
        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var service = new RagService(aiProvider, embeddingProvider, retriever, validator, calculator);

        var result = await service.AnswerQuestionAsync(analysisId, "Where is the payment service?");

        Assert.NotNull(result.ConfidenceDetails);
        Assert.Equal(AiConfidenceLevel.Low, result.Confidence);
        Assert.Equal(AiConfidenceLevel.Low, result.ConfidenceDetails.Level);
        Assert.Equal(0.0f, result.ConfidenceDetails.Score);
        Assert.Contains("unsupported by retrieved repository evidence", result.ConfidenceDetails.Rationale);
    }
}
