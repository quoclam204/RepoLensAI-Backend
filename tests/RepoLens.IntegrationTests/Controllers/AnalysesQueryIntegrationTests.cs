using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Analyses;
using RepoLens.Application.DTOs.Overview;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class AnalysesQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AnalysesQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task CreateAnalysis_WithValidGitUrl_Returns202AcceptedWithLocation()
    {
        // Arrange
        var request = new CreateAnalysisGitRequest { SourceUrl = "https://github.com/dotnet/runtime" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/analyses", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var result = await response.Content.ReadFromJsonAsync<CreateAnalysisResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.AnalysisId);
        Assert.NotEqual(Guid.Empty, result.RepositoryId);
        Assert.Equal("Created", result.Status);
        Assert.Contains(result.AnalysisId.ToString(), response.Headers.Location.ToString());
    }

    [Fact]
    public async Task CreateAnalysis_WithEmptyGitUrl_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateAnalysisGitRequest { SourceUrl = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/analyses", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WhenAnalysisExists_Returns200OkWithProgress()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnalysisStatusResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.AnalysisId, result.Id);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(100, result.Progress);
    }

    [Fact]
    public async Task GetStatus_WhenAnalysisNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetOverview_WhenAnalysisCompleted_Returns200OkWithStatistics()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/overview");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnalysisOverviewResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.AnalysisId, result.AnalysisId);
        Assert.Equal("RepoLens-Sample", result.Repository.Name);
        Assert.Equal(1, result.Statistics.Projects);
        Assert.Equal(1, result.Statistics.SourceFiles);
        Assert.Equal(1, result.Statistics.Symbols);
        Assert.Equal(1, result.Statistics.ApiEndpoints);
        Assert.Equal(1, result.Statistics.DatabaseEntities);
        Assert.NotEmpty(result.Languages);
        Assert.Contains(result.Languages, l => l.Name == "C#");
    }

    [Fact]
    public async Task GetOverview_WhenAnalysisNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{nonExistentId}/overview");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetOverview_WhenAnalysisNotReady_Returns409ConflictWithAnalysisNotReady()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedAnalyzingAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/overview");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_READY", error.Error.Code);
    }

    [Fact]
    public async Task GetOverview_WhenAnalysisFailed_Returns409ConflictWithAnalysisFailed()
    {
        // Arrange
        var analysisId = await QueryApiTestFixture.SeedFailedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analysisId}/overview");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_FAILED", error.Error.Code);
    }
}
