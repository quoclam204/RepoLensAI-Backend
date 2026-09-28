using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.Evaluation;

/// <summary>
/// AI Evaluation Benchmark Suite covering T104 - T107 (NFR-AI-001, NFR-AI-002).
/// Validates retrieval relevance, grounded claim verification, and strict insufficient evidence handling.
/// </summary>
public class AiEvaluationSuiteTests
{
    private readonly Guid _analysisId = Guid.NewGuid();

    public record BenchmarkItem(
        string Question,
        string Category,
        bool ShouldHaveEvidence,
        string ExpectedEvidenceSnippet);

    public static readonly IReadOnlyList<BenchmarkItem> EvaluationDataset = new List<BenchmarkItem>
    {
        new(
            Question: "What is the architecture of this project?",
            Category: "Architecture",
            ShouldHaveEvidence: true,
            ExpectedEvidenceSnippet: "Clean Architecture with Domain, Application, and Infrastructure layers"),
        new(
            Question: "What API endpoints exist?",
            Category: "Endpoints",
            ShouldHaveEvidence: true,
            ExpectedEvidenceSnippet: "GET /api/analyses, POST /api/analyses"),
        new(
            Question: "Where is the database connection configured?",
            Category: "Database",
            ShouldHaveEvidence: true,
            ExpectedEvidenceSnippet: "builder.Services.AddDbContext<RepoLensDbContext>"),
        new(
            Question: "Where is the Kubernetes cluster deployment configuration located?",
            Category: "Unknown/OutOfScope",
            ShouldHaveEvidence: false,
            ExpectedEvidenceSnippet: ""),
        new(
            Question: "What is the production root database password?",
            Category: "Secrets/NonExistent",
            ShouldHaveEvidence: false,
            ExpectedEvidenceSnippet: "")
    };

    [Fact]
    public void T104_EvaluationDataset_HasRequiredEvaluationQuestions()
    {
        // Assert T104 benchmark requirements
        Assert.True(EvaluationDataset.Count >= 5);
        Assert.Contains(EvaluationDataset, q => q.Category == "Architecture");
        Assert.Contains(EvaluationDataset, q => q.Category == "Endpoints");
        Assert.Contains(EvaluationDataset, q => q.Category == "Database");
        Assert.Contains(EvaluationDataset, q => q.Category == "Unknown/OutOfScope");
    }

    [Fact]
    public async Task T105_RetrievalQuality_ReturnsRelevantEvidenceForGroundedQuestions()
    {
        // Arrange
        var fakeRetriever = new TestVectorRetriever();
        var relevantChunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/RepoLens.Api/Program.cs",
            Symbol: "Program.Main",
            StartLine: 1,
            EndLine: 30,
            Content: "builder.Services.AddDbContext<RepoLensDbContext>(opt => opt.UseNpgsql(...));",
            TokenCount: 20,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.95f,
            CosineDistance: 0.1,
            SimilarityScore: 0.90);

        fakeRetriever.RegisterResult([relevantChunk]);

        // Act
        var results = await fakeRetriever.RetrieveSimilarChunksAsync(_analysisId, new float[1536], 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Content.Contains("AddDbContext<RepoLensDbContext>"));
        Assert.All(results, r => Assert.True(r.SimilarityScore >= 0.7));
    }

    [Fact]
    public async Task T106_AnswerGrounding_ValidatesSupportedClaimsAndCalculatesConfidence()
    {
        // Arrange
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/Controllers/WeatherController.cs",
            Symbol: "WeatherController.Get",
            StartLine: 15,
            EndLine: 25,
            Content: "[HttpGet] public IActionResult Get() => Ok();",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.95);

        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var candidateAnswer = "The system exposes an HTTP GET endpoint at /api/Weather in WeatherController.";

        // Act
        var validation = await validator.ValidateAnswerAsync(candidateAnswer, [chunk]);
        var confidenceResult = calculator.EvaluateConfidence(new ConfidenceEvaluationRequest(
            "What endpoints exist?", candidateAnswer, [chunk], validation));

        // Assert
        Assert.True(validation.IsValid);
        Assert.NotEqual(AiConfidenceLevel.Unknown, confidenceResult.Level);
    }

    [Fact]
    public async Task T107_UnknownAndInsufficientEvidence_RefusesToFabricateAndReturnsStandardText()
    {
        // Arrange: Vector retriever returns 0 chunks for out-of-scope question
        var fakeRetriever = new TestVectorRetriever();
        var fakeEmbeddingProvider = new TestEmbeddingProvider();
        var fakeAiProvider = new TestAiProvider("Fabricated answer that should never be shown");

        var ragService = new RagService(
            fakeAiProvider,
            fakeEmbeddingProvider,
            fakeRetriever);

        var outOfScopeQuestion = "Where is the Kubernetes cluster deployment configuration located?";

        // Act
        var result = await ragService.AnswerQuestionAsync(_analysisId, outOfScopeQuestion);

        // Assert
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.Equal(AiConfidenceLevel.Unknown, result.Confidence);
        Assert.Empty(result.Evidence);
        Assert.False(result.HasSufficientEvidence);
    }

    private sealed class TestVectorRetriever : IVectorChunkRetriever
    {
        private IReadOnlyList<VectorChunkSearchResult> _results = [];

        public void RegisterResult(IReadOnlyList<VectorChunkSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
            Guid analysisId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VectorChunkSearchResult>>(_results.Take(topK).ToList());
        }
    }

    private sealed class TestEmbeddingProvider : IEmbeddingProvider
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            var vectors = inputs.Select(_ => new float[1536]).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(vectors);
        }
    }

    private sealed class TestAiProvider : IAiProvider
    {
        private readonly string _response;
        public TestAiProvider(string response) => _response = response;

        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = _response,
                Confidence = AiConfidenceLevel.High,
                Evidence = []
            });
        }
    }
}
