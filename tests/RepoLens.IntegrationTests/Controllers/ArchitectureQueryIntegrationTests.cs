using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Architecture;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class ArchitectureQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ArchitectureQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetArchitecture_WhenCompleted_Returns200OkWithNodesAndEdges()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/architecture");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ArchitectureResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.AnalysisId, result.AnalysisId);
        Assert.NotEmpty(result.Nodes);
        Assert.Contains(result.Nodes, n => n.Name == "BillingService" && n.Type == "Project");
        Assert.NotEmpty(result.Edges);
        Assert.Contains(result.Edges, e => e.Source == data.ProjectId.ToString());
    }

    [Fact]
    public async Task GetArchitecture_WhenAnalysisNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{nonExistentId}/architecture");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetArchitecture_WhenAnalysisNotReady_Returns409Conflict()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedAnalyzingAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/architecture");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_READY", error.Error.Code);
    }

    [Fact]
    public async Task GetArchitecture_WhenAnalysisFailed_Returns409Conflict()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedFailedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/architecture");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_FAILED", error.Error.Code);
    }
}
