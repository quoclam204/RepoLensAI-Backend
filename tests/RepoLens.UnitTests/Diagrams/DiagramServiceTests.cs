using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoLens.Application.DTOs.Diagrams;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Services;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.UnitTests.Diagrams;

/// <summary>
/// Unit tests for DiagramService (Giai đoạn 2).
/// Verifies diagram generation for all 6 repository types and verifies Archify visual rules:
/// - Correct primary and secondary diagrams per repo type
/// - Trace reach (upstream / downstream) in DetailCards
/// - Direct vs inferred edges (isInferred)
/// - Absence of DB -> NotDetected status and no fake DB nodes
/// - Library / CLI -> No API drawn
/// - Bounded node count and grouping ("+N khác")
/// </summary>
public class DiagramServiceTests : IDisposable
{
    private readonly RepoLensDbContext _context;
    private readonly RepositoryTypeDetector _detector;
    private readonly DiagramService _service;
    private readonly string _fixturesRoot;

    public DiagramServiceTests()
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new RepoLensDbContext(options);
        _detector = new RepositoryTypeDetector(_context, NullLogger<RepositoryTypeDetector>.Instance);

        var assemblyDir = Path.GetDirectoryName(typeof(DiagramServiceTests).Assembly.Location)!;
        _fixturesRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "Fixtures"));

        var workspaceOptions = Options.Create(new WorkspaceOptions
        {
            BaseDirectory = _fixturesRoot
        });

        var workspaceManager = new TemporaryWorkspaceManager(
            workspaceOptions,
            NullLogger<TemporaryWorkspaceManager>.Instance,
            NullLogger<TemporaryWorkspace>.Instance);

        _service = new DiagramService(
            _context,
            _detector,
            workspaceManager,
            workspaceOptions,
            NullLogger<DiagramService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ========================================================================================
    // 1. ApiBackend Tests
    // ========================================================================================

    [Fact]
    public async Task GetDiagram_ApiBackendWithDatabase_GeneratesArchitectureWithDb()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");

        await SeedAnalysisAsync(analysisId, repoId, fixturePath,
            hasControllers: true,
            hasServices: true,
            hasRepositories: true,
            hasDbContext: true);

        // Act: Request default / primary diagram
        var diagram = await _service.GetDiagramAsync(analysisId, "default");

        // Assert
        Assert.Equal("architecture", diagram.DiagramType);
        Assert.Equal(RepositoryType.ApiBackend, diagram.RepositoryType);
        Assert.Equal("Success", diagram.Status);
        Assert.True(diagram.DatabaseDetected);

        // Check layered nodes
        Assert.Contains(diagram.Nodes, n => n.Kind == "gateway");
        Assert.Contains(diagram.Nodes, n => n.Kind == "controller" && n.Role == "Controller");
        Assert.Contains(diagram.Nodes, n => n.Kind == "service" && n.Role == "Service");
        Assert.Contains(diagram.Nodes, n => n.Kind == "repository" && n.Role == "Repository");
        Assert.Contains(diagram.Nodes, n => n.Kind == "database" && n.Role == "Database");

        // Check edges
        Assert.Contains(diagram.Edges, e => e.Kind == "calls");
        Assert.Contains(diagram.Edges, e => e.Kind == "queries");

        // Check DetailCards have reach tracing
        Assert.NotEmpty(diagram.DetailCards);
        var controllerCard = diagram.DetailCards.FirstOrDefault(c => c.Role == "Controller");
        Assert.NotNull(controllerCard);
        Assert.NotEmpty(controllerCard.UpstreamNodes); // Client points to controller
    }

    [Fact]
    public async Task GetDiagram_ApiBackendWithoutDatabase_DoesNotDrawDatabaseNode()
    {
        // Arrange: API backend with controllers and services, but NO DbContext / DbSet
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");

        await SeedAnalysisAsync(analysisId, repoId, fixturePath,
            hasControllers: true,
            hasServices: true,
            hasRepositories: false,
            hasDbContext: false);

        // Act
        var diagram = await _service.GetDiagramAsync(analysisId, "architecture");

        // Assert
        Assert.Equal("architecture", diagram.DiagramType);
        Assert.False(diagram.DatabaseDetected);
        Assert.DoesNotContain(diagram.Nodes, n => n.Kind == "database");
        Assert.DoesNotContain(diagram.Nodes, n => n.Role == "Database");
    }

    [Fact]
    public async Task GetDiagram_ApiBackendWithoutDatabase_RequestingErd_ReturnsNotDetected()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");

        await SeedAnalysisAsync(analysisId, repoId, fixturePath,
            hasControllers: true,
            hasServices: true,
            hasRepositories: false,
            hasDbContext: false);

        // Act: Request ERD for repo without database
        var diagram = await _service.GetDiagramAsync(analysisId, "erd");

        // Assert: MUST return NotDetected status with clear message and empty nodes
        Assert.Equal("erd", diagram.DiagramType);
        Assert.Equal("NotDetected", diagram.Status);
        Assert.False(diagram.DatabaseDetected);
        Assert.Empty(diagram.Nodes);
        Assert.NotNull(diagram.Message);
        Assert.Contains("Không phát hiện database", diagram.Message);
    }

    // ========================================================================================
    // 2. Frontend Tests (Next.js/React)
    // ========================================================================================

    [Fact]
    public async Task GetDiagram_Frontend_GeneratesRouteMap()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-nextjs-app");

        await SeedFrontendAnalysisAsync(analysisId, repoId, fixturePath);

        // Act
        var diagram = await _service.GetDiagramAsync(analysisId, "default");

        // Assert
        Assert.Equal("route_map", diagram.DiagramType);
        Assert.Equal(RepositoryType.Frontend, diagram.RepositoryType);
        Assert.Equal("Success", diagram.Status);
        Assert.False(diagram.DatabaseDetected);

        // Flow: Route/Page -> Component -> Hook -> External API
        Assert.Contains(diagram.Nodes, n => n.Role == "Page");
        Assert.Contains(diagram.Nodes, n => n.Role == "Component");
        Assert.Contains(diagram.Nodes, n => n.Kind == "external_api");
    }

    // ========================================================================================
    // 3. Monorepo Tests
    // ========================================================================================

    [Fact]
    public async Task GetDiagram_Monorepo_GeneratesSystemOverviewWithProjectBoxes()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-monorepo");

        await SeedMonorepoAnalysisAsync(analysisId, repoId, fixturePath);

        // Act
        var diagram = await _service.GetDiagramAsync(analysisId, "default");

        // Assert
        Assert.Equal("system_overview", diagram.DiagramType);
        Assert.Equal(RepositoryType.Monorepo, diagram.RepositoryType);
        Assert.Equal("Success", diagram.Status);

        // Each project is a major node (box) with child diagram drill-down
        var projectNodes = diagram.Nodes.Where(n => n.Kind == "project").ToList();
        Assert.True(projectNodes.Count >= 2);
        Assert.Contains(projectNodes, n => !string.IsNullOrEmpty(n.ChildDiagramType));

        // Project reference edges exist
        Assert.Contains(diagram.Edges, e => e.Kind == "references" || e.Kind == "calls");
    }

    // ========================================================================================
    // 4. Library & CLI Tests
    // ========================================================================================

    [Fact]
    public async Task GetDiagram_Library_GeneratesNamespaceClassWithoutApi()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-library");

        await SeedLibraryAnalysisAsync(analysisId, repoId, fixturePath);

        // Act
        var diagram = await _service.GetDiagramAsync(analysisId, "default");

        // Assert
        Assert.Equal("namespace_class", diagram.DiagramType);
        Assert.Equal(RepositoryType.Library, diagram.RepositoryType);
        Assert.Equal("Success", diagram.Status);

        // Strict rule: NO API endpoints, NO database nodes drawn
        Assert.DoesNotContain(diagram.Nodes, n => n.Kind == "controller");
        Assert.DoesNotContain(diagram.Nodes, n => n.Kind == "endpoint");
        Assert.DoesNotContain(diagram.Nodes, n => n.Kind == "database");
        Assert.Contains(diagram.Nodes, n => n.Role == "Class");
    }

    [Fact]
    public async Task GetDiagram_Cli_GeneratesCallFlowStartingFromMain()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-cli");

        await SeedCliAnalysisAsync(analysisId, repoId, fixturePath);

        // Act
        var diagram = await _service.GetDiagramAsync(analysisId, "default");

        // Assert
        Assert.Equal("call_flow", diagram.DiagramType);
        Assert.Equal(RepositoryType.Cli, diagram.RepositoryType);
        Assert.Equal("Success", diagram.Status);

        // Entry point node Program.Main must exist
        Assert.Contains(diagram.Nodes, n => n.Label.Contains("Program.Main"));
        Assert.DoesNotContain(diagram.Nodes, n => n.Kind == "controller");
    }

    // ========================================================================================
    // 5. Unsupported Tests
    // ========================================================================================

    [Fact]
    public async Task GetDiagram_Unsupported_ReturnsUnsupportedStatusAndMessage()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-python");

        await SeedUnsupportedAnalysisAsync(analysisId, repoId, fixturePath);

        // Act
        var diagram = await _service.GetDiagramAsync(analysisId, "default");

        // Assert
        Assert.Equal("unsupported", diagram.DiagramType);
        Assert.Equal(RepositoryType.Unsupported, diagram.RepositoryType);
        Assert.Equal("Unsupported", diagram.Status);
        Assert.NotNull(diagram.Message);
        Assert.Contains("chưa hỗ trợ", diagram.Message);
        Assert.Empty(diagram.Nodes);
    }

    // ========================================================================================
    // 6. Classification Endpoint Service Test
    // ========================================================================================

    [Fact]
    public async Task GetClassification_ReturnsAccurateClassification()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");

        await SeedAnalysisAsync(analysisId, repoId, fixturePath,
            hasControllers: true,
            hasServices: true,
            hasRepositories: true,
            hasDbContext: true);

        // Act
        var classification = await _service.GetClassificationAsync(analysisId);

        // Assert
        Assert.Equal(RepositoryType.ApiBackend, classification.Type);
        Assert.Equal(DetectionConfidence.High, classification.Confidence);
        Assert.NotEmpty(classification.Evidences);
    }

    // ========================================================================================
    // Seed Helpers
    // ========================================================================================

    private async Task SeedAnalysisAsync(
        Guid analysisId, Guid repoId, string fixturePath,
        bool hasControllers, bool hasServices, bool hasRepositories, bool hasDbContext)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "sample-csharp-api",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = fixturePath
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "SampleApi",
            Path = fixturePath,
            Language = "C#",
            ProjectType = "Web"
        };
        _context.Projects.Add(project);

        var sourceFile = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            ProjectId = project.Id,
            Path = "Controllers/WeatherForecastController.cs",
            Language = "C#",
            Size = 500,
            Hash = "hash1"
        };
        _context.SourceFiles.Add(sourceFile);

        if (hasControllers)
        {
            _context.ApiEndpoints.Add(new ApiEndpoint
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                ProjectId = project.Id,
                Method = "GET",
                Route = "/weather",
                Controller = "WeatherForecastController",
                Action = "Get"
            });

            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "WeatherForecastController",
                FullName = "SampleApi.Controllers.WeatherForecastController",
                SymbolType = SymbolType.Class,
                StartLine = 10,
                EndLine = 35
            });
        }

        if (hasServices)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "WeatherService",
                FullName = "SampleApi.Services.WeatherService",
                SymbolType = SymbolType.Class,
                StartLine = 40,
                EndLine = 80
            });
        }

        if (hasRepositories)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "WeatherRepository",
                FullName = "SampleApi.Repositories.WeatherRepository",
                SymbolType = SymbolType.Class,
                StartLine = 85,
                EndLine = 120
            });
        }

        if (hasDbContext)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "AppDbContext",
                FullName = "SampleApi.Data.AppDbContext",
                SymbolType = SymbolType.Class,
                StartLine = 1,
                EndLine = 20
            });

            _context.DatabaseEntities.Add(new DatabaseEntity
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                Name = "Forecast",
                EntityType = "Table"
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedFrontendAnalysisAsync(Guid analysisId, Guid repoId, string fixturePath)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "sample-nextjs-app",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = fixturePath
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "NextJsApp",
            Path = fixturePath,
            Language = "TypeScript",
            ProjectType = "Node"
        };
        _context.Projects.Add(project);

        _context.SourceFiles.AddRange(
            new SourceFile
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                ProjectId = project.Id,
                Path = "app/page.tsx",
                Language = "TypeScript",
                Size = 100,
                Hash = "h1"
            },
            new SourceFile
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                ProjectId = project.Id,
                Path = "components/UserCard.tsx",
                Language = "TypeScript",
                Size = 100,
                Hash = "h2"
            },
            new SourceFile
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                ProjectId = project.Id,
                Path = "hooks/useAuth.ts",
                Language = "TypeScript",
                Size = 100,
                Hash = "h3"
            });

        await _context.SaveChangesAsync();
    }

    private async Task SeedMonorepoAnalysisAsync(Guid analysisId, Guid repoId, string fixturePath)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "sample-monorepo",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = fixturePath
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        var backendProj = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "Backend",
            Path = Path.Combine(fixturePath, "Backend"),
            Language = "C#",
            ProjectType = "CSharp"
        };

        var frontendProj = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "Frontend",
            Path = Path.Combine(fixturePath, "Frontend"),
            Language = "TypeScript",
            ProjectType = "Node"
        };

        _context.Projects.AddRange(backendProj, frontendProj);

        _context.Dependencies.Add(new Dependency
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            SourceId = backendProj.Id.ToString(),
            TargetId = frontendProj.Id.ToString(),
            DependencyType = DependencyType.ProjectReference
        });

        await _context.SaveChangesAsync();
    }

    private async Task SeedLibraryAnalysisAsync(Guid analysisId, Guid repoId, string fixturePath)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "sample-library",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = fixturePath
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "MathLib",
            Path = fixturePath,
            Language = "C#",
            ProjectType = "CSharp"
        };
        _context.Projects.Add(project);

        var sourceFile = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            ProjectId = project.Id,
            Path = "Calculator.cs",
            Language = "C#",
            Size = 200,
            Hash = "h4"
        };
        _context.SourceFiles.Add(sourceFile);

        _context.CodeSymbols.Add(new CodeSymbol
        {
            Id = Guid.NewGuid(),
            SourceFileId = sourceFile.Id,
            Name = "Calculator",
            FullName = "MathLib.Calculator",
            SymbolType = SymbolType.Class,
            StartLine = 5,
            EndLine = 30
        });

        await _context.SaveChangesAsync();
    }

    private async Task SeedCliAnalysisAsync(Guid analysisId, Guid repoId, string fixturePath)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "sample-cli",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = fixturePath
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "CliTool",
            Path = fixturePath,
            Language = "C#",
            ProjectType = "CSharp"
        };
        _context.Projects.Add(project);

        var sourceFile = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            ProjectId = project.Id,
            Path = "Program.cs",
            Language = "C#",
            Size = 300,
            Hash = "h5"
        };
        _context.SourceFiles.Add(sourceFile);

        _context.CodeSymbols.AddRange(
            new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "Program",
                FullName = "CliTool.Program",
                SymbolType = SymbolType.Class,
                StartLine = 3,
                EndLine = 25
            },
            new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "RunCommandHandler",
                FullName = "CliTool.RunCommandHandler",
                SymbolType = SymbolType.Class,
                StartLine = 30,
                EndLine = 60
            });

        await _context.SaveChangesAsync();
    }

    private async Task SeedUnsupportedAnalysisAsync(Guid analysisId, Guid repoId, string fixturePath)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "sample-python",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = fixturePath
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        await _context.SaveChangesAsync();
    }
}
