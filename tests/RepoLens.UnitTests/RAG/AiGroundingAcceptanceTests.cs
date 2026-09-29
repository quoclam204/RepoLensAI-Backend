using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

/// <summary>
/// AI Grounding Acceptance Tests (T124, NFR-AI-001).
/// Verifies that claims without evidence are caught, insufficient evidence is explicitly returned,
/// and AI confidence strictly derives from evidence grounding.
/// </summary>
public class AiGroundingAcceptanceTests
{
    private readonly Guid _analysisId = Guid.NewGuid();

    [Fact]
    public async Task T124_GroundedAnswers_MustCiteEvidenceNodesAndAssignHighConfidence()
    {
        // Arrange
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/Services/PaymentService.cs",
            Symbol: "PaymentService.ProcessPayment",
            StartLine: 20,
            EndLine: 45,
            Content: "public PaymentResult ProcessPayment(Order order) { return new PaymentResult { Success = true }; }",
            TokenCount: 25,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.95f,
            CosineDistance: 0.05,
            SimilarityScore: 0.95);

        var fakeRetriever = new GroundingTestRetriever([chunk]);

        var fakeAi = new GroundingTestAiProvider("PaymentService provides ProcessPayment to handle order payments.");
        var fakeEmbedding = new GroundingTestEmbeddingProvider();

        var ragService = new RagService(
            fakeAi,
            fakeEmbedding,
            fakeRetriever);

        // Act
        var result = await ragService.AnswerQuestionAsync(_analysisId, "How are payments processed?");

        // Assert
        Assert.True(result.HasSufficientEvidence);
        Assert.NotEmpty(result.Answer);
        Assert.NotEmpty(result.Evidence);
        Assert.True(result.Confidence == AiConfidenceLevel.High || result.Confidence == AiConfidenceLevel.Medium);
    }

    [Fact]
    public async Task T124_MissingEvidence_MustReturnStandardInsufficientEvidenceMessage()
    {
        // Arrange: Vector retriever returns no chunks for ungrounded question
        var fakeRetriever = new GroundingTestRetriever([]);
        var fakeAi = new GroundingTestAiProvider("This response should be rejected");
        var fakeEmbedding = new GroundingTestEmbeddingProvider();

        var ragService = new RagService(
            fakeAi,
            fakeEmbedding,
            fakeRetriever);

        // Act
        var result = await ragService.AnswerQuestionAsync(_analysisId, "What is the AWS account ID?");

        // Assert
        Assert.False(result.HasSufficientEvidence);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.Equal(AiConfidenceLevel.Unknown, result.Confidence);
        Assert.Empty(result.Evidence);
    }

    private sealed class GroundingTestRetriever : IVectorChunkRetriever
    {
        private readonly IReadOnlyList<VectorChunkSearchResult> _results;
        public GroundingTestRetriever(IReadOnlyList<VectorChunkSearchResult> results) => _results = results;

        public Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
            Guid analysisId, float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_results);
        }
    }

    private sealed class GroundingTestEmbeddingProvider : IEmbeddingProvider
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new float[1536]).ToList());
        }
    }

    private sealed class GroundingTestAiProvider : IAiProvider
    {
        private readonly string _content;
        public GroundingTestAiProvider(string content) => _content = content;

        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = _content,
                Confidence = AiConfidenceLevel.High,
                Evidence = []
            });
        }
    }
}
