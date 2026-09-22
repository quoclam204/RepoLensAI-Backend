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
}
