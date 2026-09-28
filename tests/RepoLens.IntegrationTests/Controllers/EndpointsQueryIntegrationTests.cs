using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Endpoints;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class EndpointsQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndpointsQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetEndpoints_Returns200OkWithPagedResult()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/endpoints?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EndpointItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, e => e.Id == data.EndpointId.ToString() && e.Route == "/api/invoices");
    }

    [Fact]
    public async Task GetEndpoints_WithFilterByMethodAndRoute_ReturnsFilteredItems()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/endpoints?method=GET&route=invoices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EndpointItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.All(result.Items, e =>
        {
            Assert.Equal("GET", e.Method, ignoreCase: true);
            Assert.Contains("invoices", e.Route, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task GetEndpointDetail_WhenExists_Returns200OkWithEvidence()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/endpoints/{data.EndpointId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<EndpointDetailResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.EndpointId.ToString(), result.Id);
        Assert.Equal("GET", result.Method);
        Assert.Equal("/api/invoices", result.Route);
        Assert.Equal("InvoicesController", result.Controller);
        Assert.Equal("GetInvoices", result.Action);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public async Task GetEndpointDetail_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentEndpointId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/endpoints/{nonExistentEndpointId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ENDPOINT_NOT_FOUND", error.Error.Code);
    }
}
