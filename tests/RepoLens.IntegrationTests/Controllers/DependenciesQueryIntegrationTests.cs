using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Dependencies;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class DependenciesQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DependenciesQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetDependencies_Returns200OkWithPagedResult()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/dependencies?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DependencyItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, d => d.Id == data.DependencyId.ToString());
    }

    [Fact]
    public async Task GetDependencies_WithFilterByProjectId_ReturnsFilteredItems()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/dependencies?projectId={data.ProjectId}&direction=outbound");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DependencyItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.All(result.Items, d => Assert.Equal(data.ProjectId.ToString(), d.Source.Id));
    }

    [Fact]
    public async Task GetDependencyDetail_WhenExists_Returns200OkWithEvidence()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/dependencies/{data.DependencyId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DependencyDetailResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.DependencyId.ToString(), result.Id);
        Assert.Equal("ProjectReference", result.Type);
        Assert.Equal(data.ProjectId.ToString(), result.Source.Id);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public async Task GetDependencyDetail_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentDependencyId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/dependencies/{nonExistentDependencyId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("DEPENDENCY_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetDependencies_WhenAnalysisNotFound_Returns404NotFound()
    {
        // Arrange
        var nonExistentAnalysisId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{nonExistentAnalysisId}/dependencies");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetDependencies_WhenAnalysisNotReady_Returns409Conflict()
    {
        // Arrange
        var analyzingId = await QueryApiTestFixture.SeedAnalyzingAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{analyzingId}/dependencies");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ANALYSIS_NOT_READY", error.Error.Code);
    }
}
