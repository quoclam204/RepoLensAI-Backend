using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Evidence;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class EvidenceQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EvidenceQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetEvidences_Returns200OkWithPagedResult()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/evidence?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EvidenceDetailResponse>>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, e => e.Id == data.EvidenceId.ToString());
    }

    [Fact]
    public async Task GetEvidences_WithSymbolFilter_ReturnsFilteredEvidences()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/evidence?symbol=InvoicesController");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EvidenceDetailResponse>>(JsonOptions);
        Assert.NotNull(result);
        Assert.All(result.Items, e => Assert.Contains("InvoicesController", e.Symbol, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetEvidenceDetail_WhenExists_Returns200OkWithLineRange()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/evidence/{data.EvidenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<EvidenceDetailResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.EvidenceId.ToString(), result.Id);
        Assert.Equal("InvoicesController", result.Symbol);
        Assert.Equal(10, result.StartLine);
        Assert.Equal(20, result.EndLine);
        Assert.Equal("Declaration", result.EvidenceType);
        Assert.False(string.IsNullOrWhiteSpace(result.Description));
    }

    [Fact]
    public async Task GetEvidenceDetail_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentEvidenceId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/evidence/{nonExistentEvidenceId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("EVIDENCE_NOT_FOUND", error.Error.Code);
    }
}
