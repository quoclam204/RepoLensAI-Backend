using Microsoft.AspNetCore.Mvc;
using RepoLens.Api.Controllers;
using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Architecture;
using RepoLens.Application.Models.Archify;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.Controllers;

public class ArchitectureControllerTests
{
    [Fact]
    public async Task GetArchitecture_ReturnsOk_WhenAnalysisExists()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var fakeArchResponse = new ArchitectureResponse(
            analysisId,
            [new("proj:api", "Project", "Api", "src/Api")],
            []);

        var fakeService = new FakeArchitectureService(fakeArchResponse);
        var controller = new ArchitectureController(fakeService, new ArchifyAdapter());

        // Act
        var result = await controller.GetArchitecture(analysisId, format: null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ArchitectureResponse>(okResult.Value);
        Assert.Equal(analysisId, response.AnalysisId);
    }

    [Fact]
    public async Task GetArchitecture_WithFormatArchify_ReturnsArchifyDocument()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var fakeArchResponse = new ArchitectureResponse(
            analysisId,
            [new("proj:api", "Project", "Api", "src/Api")],
            []);

        var fakeService = new FakeArchitectureService(fakeArchResponse);
        var controller = new ArchitectureController(fakeService, new ArchifyAdapter());

        // Act
        var result = await controller.GetArchitecture(analysisId, format: "archify", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ArchifyDocument>(okResult.Value);
        Assert.NotNull(response.System);
    }

    [Fact]
    public async Task GetArchifyArchitecture_ReturnsArchifyDocumentDirectly()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var fakeArchResponse = new ArchitectureResponse(
            analysisId,
            [new("proj:api", "Project", "Api", "src/Api")],
            []);

        var fakeService = new FakeArchitectureService(fakeArchResponse);
        var controller = new ArchitectureController(fakeService, new ArchifyAdapter());

        // Act
        var result = await controller.GetArchifyArchitecture(analysisId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ArchifyDocument>(okResult.Value);
        Assert.NotNull(response.System);
    }

    [Fact]
    public async Task GetArchitecture_ReturnsNotFound_WhenAnalysisNotFound()
    {
        // Arrange
        var fakeService = new FakeArchitectureService(null);
        var controller = new ArchitectureController(fakeService, new ArchifyAdapter());

        // Act
        var result = await controller.GetArchitecture(Guid.NewGuid(), format: null, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeArchitectureService : IArchitectureService
    {
        private readonly ArchitectureResponse? _response;

        public FakeArchitectureService(ArchitectureResponse? response)
        {
            _response = response;
        }

        public Task<ArchitectureResponse?> GetArchitectureAsync(Guid analysisId, CancellationToken ct = default)
        {
            return Task.FromResult(_response);
        }
    }
}
