using RepoLens.Analysis.Graph;
using RepoLens.Analysis.RAG;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;
using DomainEvidence = RepoLens.Domain.Entities.Evidence;

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
        Assert.Equal(1.0f, chunks[0].ConfidenceScore);
    }

    [Fact]
    public void EvidencePreservationTests_EveryChunkPreservesEvidenceMetadata()
    {
        // Arrange
        var code = @"public class InvoiceService
{
    public void ProcessInvoice() {}
}";
        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "class:InvoiceService",
                name: "InvoiceService",
                type: KnowledgeNodeType.Class,
                filePath: "Services/InvoiceService.cs",
                location: new SourceLocation("Services/InvoiceService.cs", 1, 4))
        };

        var evidenceId = Guid.NewGuid();
        var existingEvidences = new List<DomainEvidence>
        {
            new DomainEvidence
            {
                Id = evidenceId,
                AnalysisId = Guid.NewGuid(),
                FilePath = "Services/InvoiceService.cs",
                StartLine = 1,
                EndLine = 4,
                EvidenceType = EvidenceType.Declaration,
                Description = "Invoice service declaration",
                Confidence = ConfidenceScore.High
            }
        };

        var fileContents = new Dictionary<string, string>
        {
            ["Services/InvoiceService.cs"] = code
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks(nodes, fileContents, existingEvidences);

        // Assert
        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.NotNull(chunk.EvidenceKey);
        Assert.Equal("ev:Services/InvoiceService.cs:1-4", chunk.EvidenceKey);
        Assert.NotNull(chunk.EvidenceIds);
        Assert.Contains(evidenceId, chunk.EvidenceIds);
        Assert.True(chunk.ConfidenceScore >= 0.85f);
    }

    [Fact]
    public void SourceLocationPreservationTests_PreservesFilePathAndExactLineRanges()
    {
        // Arrange
        var lines = new List<string>();
        for (int i = 1; i <= 50; i++)
        {
            lines.Add($"// Line {i}: code here");
        }
        var code = string.Join("\n", lines);

        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "method:DoWork",
                name: "DoWork",
                type: KnowledgeNodeType.Method,
                filePath: "src/Worker.cs",
                location: new SourceLocation("src/Worker.cs", 10, 25))
        };

        var fileContents = new Dictionary<string, string>
        {
            ["src/Worker.cs"] = code
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks(nodes, fileContents);

        // Assert
        Assert.Single(chunks);
        Assert.Equal("src/Worker.cs", chunks[0].FilePath);
        Assert.Equal(10, chunks[0].StartLine);
        Assert.Equal(25, chunks[0].EndLine);
        Assert.Equal("DoWork", chunks[0].Symbol);
    }

    [Fact]
    public void MultipleEvidenceTests_PreservesAllOverlappingEvidenceRecords()
    {
        // Arrange
        var code = @"public class PaymentService
{
    public void Authorize() {}
    public void Capture() {}
}";
        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "class:PaymentService",
                name: "PaymentService",
                type: KnowledgeNodeType.Class,
                filePath: "Services/PaymentService.cs",
                location: new SourceLocation("Services/PaymentService.cs", 1, 5))
        };

        var evi1 = new DomainEvidence
        {
            Id = Guid.NewGuid(),
            AnalysisId = Guid.NewGuid(),
            FilePath = "Services/PaymentService.cs",
            StartLine = 1,
            EndLine = 5,
            EvidenceType = EvidenceType.Declaration
        };
        var evi2 = new DomainEvidence
        {
            Id = Guid.NewGuid(),
            AnalysisId = Guid.NewGuid(),
            FilePath = "Services/PaymentService.cs",
            StartLine = 3,
            EndLine = 3,
            EvidenceType = EvidenceType.Invocation
        };

        var fileContents = new Dictionary<string, string>
        {
            ["Services/PaymentService.cs"] = code
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks(nodes, fileContents, [evi1, evi2]);

        // Assert
        Assert.Single(chunks);
        var chunk = chunks[0];
        Assert.NotNull(chunk.EvidenceIds);
        Assert.Equal(2, chunk.EvidenceIds.Count);
        Assert.Contains(evi1.Id, chunk.EvidenceIds);
        Assert.Contains(evi2.Id, chunk.EvidenceIds);
    }

    [Fact]
    public void LargeDocumentTests_SplitsLargeSourceIntoSubChunksPreservingLineBoundaries()
    {
        // Arrange: Generate 100 lines of 50 chars each = ~5000 characters
        var lines = Enumerable.Range(1, 100)
            .Select(i => $"public void Method{i}() {{ Console.WriteLine({i}); }}")
            .ToList();
        var code = string.Join("\n", lines);

        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "class:HugeClass",
                name: "HugeClass",
                type: KnowledgeNodeType.Class,
                filePath: "HugeClass.cs",
                location: new SourceLocation("HugeClass.cs", 1, 100))
        };

        var fileContents = new Dictionary<string, string>
        {
            ["HugeClass.cs"] = code
        };

        var options = new DocumentChunkOptions
        {
            MaxChunkChars = 1000 // Small limit to trigger splitting
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks(nodes, fileContents, existingEvidences: null, options);

        // Assert
        Assert.True(chunks.Count > 1, "Large document must be split into multiple chunks");

        // Verify ordering, indices and line continuity
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            Assert.Equal(i, chunk.ChunkIndex);
            Assert.True(chunk.Content.Length <= options.MaxChunkChars);
            Assert.True(chunk.StartLine > 0);
            Assert.True(chunk.EndLine >= chunk.StartLine);
            Assert.False(chunk.Content.EndsWith("..."), "Chunks must not be arbitrarily truncated with dots");
            Assert.Equal("HugeClass", chunk.Symbol);
        }

        // First chunk starts at 1, last chunk ends at 100
        Assert.Equal(1, chunks.First().StartLine);
        Assert.Equal(100, chunks.Last().EndLine);
    }

    [Fact]
    public void DeterministicChunkTests_ProducesIdenticalChunksGivenSameInput()
    {
        // Arrange
        var fileContent = "public class A { public void Foo() {} }\npublic class B { public void Bar() {} }";
        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create("class:B", "B", KnowledgeNodeType.Class, "File.cs", new SourceLocation("File.cs", 2, 2)),
            KnowledgeNode.Create("class:A", "A", KnowledgeNodeType.Class, "File.cs", new SourceLocation("File.cs", 1, 1))
        };
        var fileContents = new Dictionary<string, string>
        {
            ["File.cs"] = fileContent
        };

        // Act
        var run1 = DocumentChunkGenerator.GenerateChunks(nodes, fileContents);
        var run2 = DocumentChunkGenerator.GenerateChunks(nodes, fileContents);

        // Assert
        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].ChunkIndex, run2[i].ChunkIndex);
            Assert.Equal(run1[i].FilePath, run2[i].FilePath);
            Assert.Equal(run1[i].Symbol, run2[i].Symbol);
            Assert.Equal(run1[i].StartLine, run2[i].StartLine);
            Assert.Equal(run1[i].EndLine, run2[i].EndLine);
            Assert.Equal(run1[i].Content, run2[i].Content);
            Assert.Equal(run1[i].EvidenceKey, run2[i].EvidenceKey);
        }
    }

    [Fact]
    public void EmptySourceTests_HandlesEmptyAndWhitespaceFilesGracefullyWithoutCrash()
    {
        // Arrange
        var fileContents = new Dictionary<string, string>
        {
            ["empty.txt"] = "",
            ["whitespace.cs"] = "   \r\n  \t  \n  "
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks([], fileContents);

        // Assert
        Assert.Empty(chunks);
    }

    [Fact]
    public void DocumentationChunkTests_ChunksMarkdownByHeadingsWithSectionSymbols()
    {
        // Arrange
        var markdown = @"# Project Title
Welcome to the documentation.

## Architecture
This section describes the Clean Architecture.
It has several components.

## Database Design
Entity Relationship diagrams go here.
";
        var fileContents = new Dictionary<string, string>
        {
            ["docs/architecture.md"] = markdown
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks([], fileContents);

        // Assert
        Assert.Equal(3, chunks.Count);

        Assert.Equal("# Project Title", chunks[0].Symbol);
        Assert.Equal(1, chunks[0].StartLine);
        Assert.Contains("Welcome to the documentation", chunks[0].Content);

        Assert.Equal("## Architecture", chunks[1].Symbol);
        Assert.Equal(4, chunks[1].StartLine);
        Assert.Contains("Clean Architecture", chunks[1].Content);

        Assert.Equal("## Database Design", chunks[2].Symbol);
        Assert.Equal(8, chunks[2].StartLine);
        Assert.Contains("Entity Relationship", chunks[2].Content);
    }

    [Fact]
    public void SecretMaskingTests_MasksCredentialsAndTokensInGeneratedChunks()
    {
        // Arrange
        var sensitiveSource = @"public class DbConfig
{
    public string ConnectionString = ""Server=db;Database=app;Uid=admin;Password=SuperSecret123;"";
    public string ApiToken = ""Bearer sk-proj-12345abcdef"";
}";
        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "class:DbConfig",
                name: "DbConfig",
                type: KnowledgeNodeType.Class,
                filePath: "Config/DbConfig.cs",
                location: new SourceLocation("Config/DbConfig.cs", 1, 5))
        };
        var fileContents = new Dictionary<string, string>
        {
            ["Config/DbConfig.cs"] = sensitiveSource
        };

        // Act
        var chunks = DocumentChunkGenerator.GenerateChunks(nodes, fileContents);

        // Assert
        Assert.Single(chunks);
        var content = chunks[0].Content;
        Assert.DoesNotContain("SuperSecret123", content);
        Assert.DoesNotContain("sk-proj-12345abcdef", content);
        Assert.Contains("***MASKED***", content);
    }
}
