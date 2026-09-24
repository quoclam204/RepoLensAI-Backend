using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Services;

namespace RepoLens.UnitTests.RAG;

public class EvidenceRetrieverTests
{
    private static RepoLensDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseInMemoryDatabase(databaseName: "RepoLens_EvidenceRetriever_" + Guid.NewGuid().ToString("N"))
            .Options;

        return new RepoLensDbContext(options);
    }

    [Fact]
    public async Task RetrieveGroundedEvidenceAsync_WhenMatchingEvidenceExists_ReturnsGroundedResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var analysisId = Guid.NewGuid();

        var evidence = new Evidence
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            FilePath = "src/Controllers/ContractController.cs",
            Symbol = "ContractController",
            StartLine = 15,
            EndLine = 25,
            EvidenceType = EvidenceType.Declaration,
            Description = "public class ContractController : ControllerBase { private readonly IContractService _service; }",
            Confidence = ConfidenceScore.High
        };

        context.Evidences.Add(evidence);
        await context.SaveChangesAsync();

        var retriever = new EvidenceRetriever(context);

        // Act
        var result = await retriever.RetrieveGroundedEvidenceAsync(
            analysisId: analysisId,
            question: "What service does ContractController call?",
            targetSymbolOrPath: "ContractController");

        // Assert
        Assert.True(result.HasSufficientEvidence);
        Assert.Equal("Grounded", result.GroundingStatus);
        Assert.Single(result.Items);
        Assert.Equal("src/Controllers/ContractController.cs", result.Items[0].FilePath);
        Assert.Equal(15, result.Items[0].StartLine);
        Assert.Equal(25, result.Items[0].EndLine);
    }

    [Fact]
    public async Task RetrieveGroundedEvidenceAsync_WhenNoEvidenceExists_ReturnsInsufficientEvidence()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var analysisId = Guid.NewGuid();
        var retriever = new EvidenceRetriever(context);

        // Act
        var result = await retriever.RetrieveGroundedEvidenceAsync(
            analysisId: analysisId,
            question: "What service does UnknownController call?",
            targetSymbolOrPath: "UnknownController");

        // Assert
        Assert.False(result.HasSufficientEvidence);
        Assert.Equal("Insufficient evidence", result.GroundingStatus);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RetrieveDocumentChunksAsync_StrictlyEnforcesAnalysisIsolation()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var analysis1 = Guid.NewGuid();
        var analysis2 = Guid.NewGuid();

        var sourceFile1 = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis1,
            Path = "src/Service1.cs",
            Language = "csharp",
            Hash = "hash1"
        };
        var sourceFile2 = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis2,
            Path = "src/Service2.cs",
            Language = "csharp",
            Hash = "hash2"
        };
        context.SourceFiles.AddRange(sourceFile1, sourceFile2);

        var chunk1 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis1,
            SourceFileId = sourceFile1.Id,
            Content = "public class Service1 {}",
            TokenCount = 10,
            ChunkIndex = 0
        };
        var chunk2 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis2,
            SourceFileId = sourceFile2.Id,
            Content = "public class Service2 {}",
            TokenCount = 10,
            ChunkIndex = 0
        };
        context.DocumentChunks.AddRange(chunk1, chunk2);
        await context.SaveChangesAsync();

        var retriever = new EvidenceRetriever(context);

        // Act: Query for Analysis 1 only
        var retrieved1 = await retriever.RetrieveDocumentChunksAsync(analysis1);

        // Assert: Chunks from Analysis 2 MUST NEVER leak into Analysis 1
        Assert.Single(retrieved1);
        Assert.Equal(chunk1.Id, retrieved1[0].ChunkId);
        Assert.Equal("src/Service1.cs", retrieved1[0].FilePath);
        Assert.DoesNotContain(retrieved1, c => c.ChunkId == chunk2.Id);
    }

    [Fact]
    public async Task RetrieveDocumentChunksAsync_PreservesCompleteProvenance()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var analysisId = Guid.NewGuid();

        var file = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Path = "src/OrderService.cs",
            Language = "csharp",
            Hash = "hash"
        };
        context.SourceFiles.Add(file);

        var evidence = new Evidence
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            FilePath = "src/OrderService.cs",
            Symbol = "OrderService",
            StartLine = 10,
            EndLine = 40,
            EvidenceType = EvidenceType.CodeSymbol,
            Description = "class OrderService declaration",
            Confidence = ConfidenceScore.High
        };
        context.Evidences.Add(evidence);

        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            SourceFileId = file.Id,
            EvidenceId = evidence.Id,
            Content = "public class OrderService { ... }",
            TokenCount = 15,
            ChunkIndex = 0
        };
        context.DocumentChunks.Add(chunk);
        await context.SaveChangesAsync();

        var retriever = new EvidenceRetriever(context);

        // Act
        var chunks = await retriever.RetrieveDocumentChunksAsync(
            analysisId: analysisId,
            targetSymbolOrPath: "OrderService");

        // Assert provenance answers:
        // 1. Where did this come from? -> FilePath
        // 2. Which file? -> FilePath
        // 3. Which lines? -> StartLine to EndLine
        // 4. Which evidence? -> EvidenceId
        // 5. What confidence? -> ConfidenceScore
        Assert.Single(chunks);
        var item = chunks[0];
        Assert.Equal("src/OrderService.cs", item.FilePath);
        Assert.Equal(10, item.StartLine);
        Assert.Equal(40, item.EndLine);
        Assert.Equal(evidence.Id, item.EvidenceId);
        Assert.Equal("OrderService", item.Symbol);
        Assert.True(item.ConfidenceScore >= 0.85f);
    }
}
