using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Diagrams;
using RepoLens.Application.Models.Classification;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.IntegrationTests.Fixtures;
using AnalysisEntity = RepoLens.Domain.Entities.Analysis;

namespace RepoLens.IntegrationTests.Controllers;

public class DiagramsQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public DiagramsQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetClassification_WhenCompleted_Returns200OkWithClassification()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/classification");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RepositoryClassification>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(RepositoryType.ApiBackend, result.Type);
        Assert.Contains("C#", result.DetectedLanguages);
        Assert.Equal(DetectionConfidence.High, result.Confidence);
        Assert.NotEmpty(result.Evidences);
        Assert.NotNull(result.Summary);
    }

    [Fact]
    public async Task GetClassification_WhenAnalysisNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{nonExistentId}/classification");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetClassification_WhenAnalysisNotReady_Returns409Conflict()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedAnalyzingAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/classification");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_READY", error.Error.Code);
    }

    [Fact]
    public async Task GetClassification_WhenAnalysisFailed_Returns409Conflict()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedFailedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/classification");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_FAILED", error.Error.Code);
    }

    [Fact]
    public async Task GetDiagram_WhenCompleted_Returns200OkWithDefaultArchitectureDiagram()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/diagrams");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("architecture", result.DiagramType);
        Assert.Equal(RepositoryType.ApiBackend, result.RepositoryType);
        Assert.Equal("Success", result.Status);
        Assert.NotEmpty(result.Nodes);
        Assert.NotEmpty(result.Edges);
        Assert.NotEmpty(result.DetailCards);
    }

    [Fact]
    public async Task GetDiagram_WithSpecificType_ReturnsSpecifiedDiagram()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/diagrams/endpoints");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("endpoints", result.DiagramType);
        Assert.Equal(RepositoryType.ApiBackend, result.RepositoryType);
        Assert.Equal("Success", result.Status);
    }

    [Fact]
    public async Task GetDiagram_WhenAnalysisNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{nonExistentId}/diagrams");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetDiagram_WhenAnalysisNotReady_Returns409Conflict()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedAnalyzingAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/diagrams");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_READY", error.Error.Code);
    }

    [Fact]
    public async Task DataIsolation_BetweenDifferentAnalyses_EnsuresDiagramsDoNotLeakCrossData()
    {
        // Arrange - Seed Analysis 1 (Billing)
        var analysis1 = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Seed Analysis 2 (Shipping) with distinct names
        var repo2Id = Guid.NewGuid();
        var analysis2Id = Guid.NewGuid();
        var proj2Id = Guid.NewGuid();
        var file2Id = Guid.NewGuid();
        var symbol2Id = Guid.NewGuid();

        await _factory.SeedAsync(async context =>
        {
            var repo2 = new Repository
            {
                Id = repo2Id,
                Name = "ShippingService-Repo",
                SourceType = RepositorySourceType.GitUrl,
                SourceLocation = "https://github.com/org/shipping-sample",
                Status = RepositoryStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Repositories.Add(repo2);

            var analysis2 = new AnalysisEntity
            {
                Id = analysis2Id,
                RepositoryId = repo2Id,
                Status = AnalysisStatus.Completed,
                CurrentStage = "Completed",
                CommitHash = "9876543210fedcba",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                CompletedAt = DateTimeOffset.UtcNow
            };
            context.Analyses.Add(analysis2);

            var proj2 = new Project
            {
                Id = proj2Id,
                AnalysisId = analysis2Id,
                Name = "ShippingService",
                Path = "src/ShippingService",
                Language = "C#",
                ProjectType = "WebApi"
            };
            context.Projects.Add(proj2);

            var file2 = new SourceFile
            {
                Id = file2Id,
                AnalysisId = analysis2Id,
                ProjectId = proj2Id,
                Path = "src/ShippingService/Controllers/ShipmentsController.cs",
                Language = "C#",
                Size = 1024,
                AnalysisStatus = FileAnalysisStatus.Analyzed
            };
            context.SourceFiles.Add(file2);

            var symbol2 = new CodeSymbol
            {
                Id = symbol2Id,
                SourceFileId = file2Id,
                Name = "ShipmentsController",
                FullName = "ShippingService.Controllers.ShipmentsController",
                SymbolType = SymbolType.Class,
                StartLine = 1,
                EndLine = 30
            };
            context.CodeSymbols.Add(symbol2);

            await context.SaveChangesAsync();
        });

        // Act: Query diagram for Analysis 1
        var resp1 = await _client.GetAsync($"/api/analyses/{analysis1.AnalysisId}/diagrams");
        var diagram1 = await resp1.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);

        // Query diagram for Analysis 2
        var resp2 = await _client.GetAsync($"/api/analyses/{analysis2Id}/diagrams");
        var diagram2 = await resp2.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);

        // Assert: Diagram 1 must contain BillingService and must NOT contain ShippingService or ShipmentsController
        Assert.NotNull(diagram1);
        Assert.Contains(diagram1.Nodes, n => n.Label.Contains("Billing") || n.Label.Contains("Invoices"));
        Assert.DoesNotContain(diagram1.Nodes, n => n.Label.Contains("Shipping") || n.Label.Contains("Shipments"));

        // Assert: Diagram 2 must contain ShippingService and must NOT contain BillingService or InvoicesController
        Assert.NotNull(diagram2);
        Assert.Contains(diagram2.Nodes, n => n.Label.Contains("Shipping") || n.Label.Contains("Shipments"));
        Assert.DoesNotContain(diagram2.Nodes, n => n.Label.Contains("Billing") || n.Label.Contains("Invoices"));
    }
}
