using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Files;
using RepoLens.Application.DTOs.Symbols;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Controllers;

public class FilesAndSymbolsQueryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FilesAndSymbolsQueryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateHttpsClient();
    }

    [Fact]
    public async Task GetFiles_Returns200OkWithPagedResult()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/files?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<FileItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, f => f.Id == data.SourceFileId.ToString());
    }

    [Fact]
    public async Task GetFiles_WithLanguageFilter_ReturnsFilteredFiles()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/files?language=C#");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<FileItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.All(result.Items, f => Assert.Equal("C#", f.Language, ignoreCase: true));
    }

    [Fact]
    public async Task GetFileDetail_WhenExists_Returns200OkWithSymbols()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/files/{data.SourceFileId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FileDetailResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.SourceFileId.ToString(), result.Id);
        Assert.Contains("InvoicesController.cs", result.Path);
        Assert.NotEmpty(result.Symbols);
        Assert.Contains(result.Symbols, s => s.Name == "InvoicesController");
    }

    [Fact]
    public async Task GetFileDetail_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/files/{nonExistentFileId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("FILE_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetFileContent_WhenExists_Returns200OkWithContent()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/files/{data.SourceFileId}/content");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FileContentResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.SourceFileId.ToString(), result.FileId);
        Assert.False(string.IsNullOrWhiteSpace(result.Content));
        Assert.True(result.LineCount >= 1);
    }

    [Fact]
    public async Task GetFileContent_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/files/{nonExistentFileId}/content");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("FILE_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetSymbolDetail_WhenExists_Returns200OkWithLocation()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/symbols/{data.SymbolId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SymbolDetailResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(data.SymbolId.ToString(), result.Id);
        Assert.Equal("InvoicesController", result.Name);
        Assert.Equal("Class", result.Type);
        Assert.NotNull(result.File);
        Assert.Equal(10, result.StartLine);
        Assert.Equal(60, result.EndLine);
    }

    [Fact]
    public async Task GetSymbolDetail_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        var data = await QueryApiTestFixture.SeedCompletedAnalysisAsync(_factory);
        var nonExistentSymbolId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/analyses/{data.AnalysisId}/symbols/{nonExistentSymbolId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("SYMBOL_NOT_FOUND", error.Error.Code);
    }
}
