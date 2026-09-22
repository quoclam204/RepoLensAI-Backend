using RepoLens.Analysis.Graph;
using RepoLens.Analysis.RAG;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.RAG;

public class DocumentChunkGeneratorTests
{
    [Fact]
    public void GenerateChunks_FromSymbolsAndSourceFiles_ProducesGroundedChunksWithLineSpans()
    {
        // Arrange
        var fileContent = @"using System;

namespace SampleApp.Services;

public class OrderService
{
    public void PlaceOrder(int orderId)
    {
        Console.WriteLine(orderId);
    }
}
";
        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "class:SampleApp.Services.OrderService",
                name: "OrderService",
                type: KnowledgeNodeType.Class,
                filePath: "Services/OrderService.cs",
                location: new SourceLocation("Services/OrderService.cs", 5, 12)),
            KnowledgeNode.Create(
                id: "method:SampleApp.Services.OrderService.PlaceOrder",
                name: "PlaceOrder",
                type: KnowledgeNodeType.Method,
                filePath: "Services/OrderService.cs",
                location: new SourceLocation("Services/OrderService.cs", 7, 10))
        };

        var fileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Services/OrderService.cs"] = fileContent
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks(nodes, fileContents);

        // Assert
        Assert.Equal(2, chunks.Count);

        var classChunk = chunks.First(c => c.Symbol == "OrderService");
        Assert.Equal("Services/OrderService.cs", classChunk.FilePath);
        Assert.Equal(5, classChunk.StartLine);
        Assert.Equal(12, classChunk.EndLine);
        Assert.True(classChunk.TokenCount > 0);
        Assert.StartsWith("ev:Services/OrderService.cs:5-12", classChunk.EvidenceKey);
        Assert.Contains("public class OrderService", classChunk.Content);

        var methodChunk = chunks.First(c => c.Symbol == "PlaceOrder");
        Assert.Equal(7, methodChunk.StartLine);
        Assert.Equal(10, methodChunk.EndLine);
        Assert.Contains("PlaceOrder", methodChunk.Content);
        Assert.StartsWith("ev:Services/OrderService.cs:7-10", methodChunk.EvidenceKey);
    }

    [Fact]
    public void GenerateChunks_ForFileWithoutExtractedSymbols_ProducesWholeFileChunk()
    {
        // Arrange
        var configText = "{ \"Logging\": { \"LogLevel\": { \"Default\": \"Information\" } } }";
        var fileContents = new Dictionary<string, string>
        {
            ["appsettings.json"] = configText
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks([], fileContents);

        // Assert
        Assert.Single(chunks);
        Assert.Equal("appsettings.json", chunks[0].FilePath);
        Assert.Null(chunks[0].Symbol);
        Assert.Equal(1, chunks[0].StartLine);
        Assert.Contains("Logging", chunks[0].Content);
    }
}
