using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

public class RagPromptBuilderTests
{
    [Fact]
    public void DefaultSystemPrompt_ContainsStrictGroundingDirectives()
    {
        var prompt = RagPromptBuilder.DefaultSystemPrompt;

        Assert.Contains("sole source of truth", prompt);
        Assert.Contains("Do NOT fabricate", prompt);
        Assert.Contains("insufficient to answer", prompt);
        Assert.Contains("Cite the exact file paths", prompt);
        Assert.Contains("Static analysis establishes what actually exists", prompt);
    }

    [Fact]
    public void BuildContext_WhenChunksAreNullOrEmpty_ReturnsExplicitNoEvidenceMessage()
    {
        var analysisId = Guid.NewGuid();

        var contextNull = RagPromptBuilder.BuildContext(null!, analysisId);
        var contextEmpty = RagPromptBuilder.BuildContext([], analysisId);

        Assert.Contains("No relevant repository evidence chunks were found", contextNull);
        Assert.Contains(analysisId.ToString(), contextNull);
        Assert.Contains("No relevant repository evidence chunks were found", contextEmpty);
        Assert.Contains(analysisId.ToString(), contextEmpty);
    }

    [Fact]
    public void BuildContext_WhenChunksArePresent_IncludesAllMetadataAndContent()
    {
        var analysisId = Guid.NewGuid();
        var chunks = new List<VectorChunkSearchResult>
        {
            new(
                ChunkId: Guid.NewGuid(),
                AnalysisId: analysisId,
                SourceFileId: Guid.NewGuid(),
                FilePath: "src/Services/OrderService.cs",
                Symbol: "OrderService.ProcessOrder",
                StartLine: 20,
                EndLine: 45,
                Content: "public void ProcessOrder(Order order) { _repo.Save(order); }",
                TokenCount: 15,
                ChunkIndex: 0,
                EvidenceId: Guid.NewGuid(),
                ConfidenceScore: 0.95f,
                CosineDistance: 0.12,
                SimilarityScore: 0.88),
            new(
                ChunkId: Guid.NewGuid(),
                AnalysisId: analysisId,
                SourceFileId: null,
                FilePath: "src/Models/Order.cs",
                Symbol: null,
                StartLine: 1,
                EndLine: 12,
                Content: "public record Order(Guid Id, decimal Amount);",
                TokenCount: 10,
                ChunkIndex: 1,
                EvidenceId: null,
                ConfidenceScore: 0.80f,
                CosineDistance: 0.25,
                SimilarityScore: 0.75)
        };

        var context = RagPromptBuilder.BuildContext(chunks, analysisId);

        Assert.Contains("[Chunk 1]", context);
        Assert.Contains("File: src/Services/OrderService.cs", context);
        Assert.Contains("Lines: 20-45", context);
        Assert.Contains("Symbol: OrderService.ProcessOrder", context);
        Assert.Contains("Similarity Score: 88.0%", context);
        Assert.Contains("Confidence: 0.95", context);
        Assert.Contains("public void ProcessOrder(Order order)", context);

        Assert.Contains("[Chunk 2]", context);
        Assert.Contains("File: src/Models/Order.cs", context);
        Assert.Contains("Lines: 1-12", context);
        Assert.Contains("public record Order(Guid Id, decimal Amount);", context);
    }
}
