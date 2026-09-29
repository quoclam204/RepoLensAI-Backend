using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Database;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class DatabaseQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DatabaseQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetDatabaseModel_Returns200OkWithEntitiesAndRelationships()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/database");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DatabaseModelResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entities);
        Assert.Contains(result.Entities, e => e.Name == "Invoice" && e.Type == "Table");
        Assert.NotEmpty(result.Relationships);
        Assert.Contains(result.Relationships, r => r.SourceEntityId == data.DatabaseEntityId.ToString());
    }

    [Fact]
    public async Task GetEntityDetail_WhenExists_Returns200OkWithRelationshipsAndEvidence()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/database/entities/{data.DatabaseEntityId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DatabaseEntityDetailResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.DatabaseEntityId.ToString(), result.Id);
        Assert.Equal("Invoice", result.Name);
        Assert.Equal("Table", result.Type);
        Assert.NotNull(result.Source);
        Assert.Contains("InvoicesController.cs", result.Source.File);
        Assert.NotEmpty(result.Relationships);
    }

    [Fact]
    public async Task GetEntityDetail_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentEntityId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/database/entities/{nonExistentEntityId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ENTITY_NOT_FOUND", error.Error.Code);
    }
}
