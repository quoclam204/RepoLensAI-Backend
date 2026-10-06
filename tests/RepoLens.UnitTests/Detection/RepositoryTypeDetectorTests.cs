using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Services;

namespace RepoLens.UnitTests.Detection;

/// <summary>
/// Unit tests for RepositoryTypeDetector (Giai đoạn 1).
/// Tests the 3-layer evidence detection and classification logic.
/// Uses real fixture directories for Layer 1, and InMemory EF Core for Layers 2-3.
/// </summary>
public class RepositoryTypeDetectorTests : IDisposable
{
    private readonly RepoLensDbContext _context;
    private readonly RepositoryTypeDetector _detector;
    private readonly string _fixturesRoot;

    public RepositoryTypeDetectorTests()
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new RepoLensDbContext(options);
        _detector = new RepositoryTypeDetector(_context, NullLogger<RepositoryTypeDetector>.Instance);

        // Resolve fixtures root relative to test assembly location
        var assemblyDir = Path.GetDirectoryName(typeof(RepositoryTypeDetectorTests).Assembly.Location)!;
        _fixturesRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "Fixtures"));
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ========================================================================================
    // Layer 1 Tests (File Markers Only — no DB data)
    // ========================================================================================

    [Fact]
    public async Task Detect_CSharpApiFixture_Layer1_DetectsApiBackendOrCSharp()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");
        var analysisId = Guid.NewGuid(); // No DB data seeded

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Contains("C#", result.DetectedLanguages);
        Assert.NotEmpty(result.Evidences);
        Assert.Contains(result.Evidences, e => e.Layer == 1);
        Assert.Contains(result.Evidences, e => e.Reason.Contains(".NET project file"));
    }

    [Fact]
    public async Task Detect_NextJsFixture_Layer1_DetectsTypeScriptAndNextJs()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-nextjs-app");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Contains("TypeScript", result.DetectedLanguages);
        Assert.Contains(result.Evidences, e => e.Reason.Contains("Next.js"));
        Assert.Contains(result.Evidences, e => e.Reason.Contains("package.json"));
    }

    [Fact]
    public async Task Detect_LibraryFixture_Layer1_DetectsCSharp()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-library");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Contains("C#", result.DetectedLanguages);
        Assert.Contains(result.Evidences, e => e.Layer == 1);
    }

    [Fact]
    public async Task Detect_CliFixture_Layer1_DetectsCSharp()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-cli");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Contains("C#", result.DetectedLanguages);
        Assert.Contains(result.Evidences, e => e.Layer == 1);
    }

    [Fact]
    public async Task Detect_MonorepoFixture_Layer1_DetectsMultipleLanguages()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-monorepo");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Contains("C#", result.DetectedLanguages);
        // Frontend subdir has package.json with Next.js
        Assert.Contains("JavaScript", result.DetectedLanguages);
        Assert.Contains(result.Evidences, e => e.Reason.Contains(".sln") || e.Reason.Contains(".NET"));
        Assert.Contains(result.Evidences, e => e.Reason.Contains("package.json"));
    }

    [Fact]
    public async Task Detect_PythonFixture_Layer1_DetectsUnsupported()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-python");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Equal(RepositoryType.Unsupported, result.Type);
        Assert.Equal(DetectionConfidence.High, result.Confidence);
        Assert.Contains("Python", result.DetectedLanguages);
        Assert.Contains(result.Summary, "unsupported");
    }

    [Fact]
    public async Task Detect_NonExistentPath_ReturnsUnsupported()
    {
        // Arrange
        var fakePath = Path.Combine(_fixturesRoot, "does-not-exist");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fakePath);

        // Assert
        Assert.Equal(RepositoryType.Unsupported, result.Type);
        Assert.Equal(DetectionConfidence.Unknown, result.Confidence);
    }

    // ========================================================================================
    // Layer 1+2 Tests (File Markers + DB data)
    // ========================================================================================

    [Fact]
    public async Task Detect_CSharpApi_WithDbData_DetectsApiBackendHighConfidence()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        await SeedAnalysisData(analysisId, repoId, seedApiEndpoints: true, seedControllers: true, seedDbContext: true);

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Equal(RepositoryType.ApiBackend, result.Type);
        Assert.Equal(DetectionConfidence.High, result.Confidence);
        Assert.Contains("C#", result.DetectedLanguages);
        Assert.Contains(result.Evidences, e => e.Layer == 2 && e.Reason.Contains("API endpoint"));
        Assert.Contains(result.Evidences, e => e.Layer == 2 && e.Reason.Contains("controller class"));
        Assert.Contains(result.Evidences, e => e.Layer == 2 && e.Reason.Contains("DbContext"));
    }

    [Fact]
    public async Task Detect_Library_WithDbData_DetectsLibrary()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-library");
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        // Seed only public classes, no entry point, no controllers, no endpoints
        await SeedAnalysisData(analysisId, repoId, seedPublicClasses: true);

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Equal(RepositoryType.Library, result.Type);
        Assert.Contains(result.Evidences, e => e.Layer == 2 && e.Reason.Contains("suggests library"));
    }

    [Fact]
    public async Task Detect_Cli_WithDbData_DetectsCli()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-cli");
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        // Seed entry point (Program class) but no controllers/endpoints
        await SeedAnalysisData(analysisId, repoId, seedEntryPoint: true);

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Equal(RepositoryType.Cli, result.Type);
        Assert.Contains(result.Evidences, e => e.Layer == 2 && e.Reason.Contains("Program class"));
    }

    [Fact]
    public async Task Detect_NextJs_WithDbData_DetectsFrontend()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-nextjs-app");
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        // Seed frontend route files
        await SeedAnalysisData(analysisId, repoId, seedFrontendRoutes: true);

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Equal(RepositoryType.Frontend, result.Type);
        Assert.True(result.Confidence >= DetectionConfidence.Medium);
    }

    // ========================================================================================
    // Layer 3 Tests (Relationships)
    // ========================================================================================

    [Fact]
    public async Task Detect_Monorepo_WithMultipleProjects_DetectsMonorepo()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-monorepo");
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        // Seed multiple projects with project references
        await SeedMonorepoAnalysisData(analysisId, repoId);

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        Assert.Equal(RepositoryType.Monorepo, result.Type);
        Assert.Contains(result.Evidences, e => e.Layer == 3);
    }

    // ========================================================================================
    // Evidence Traceability Tests
    // ========================================================================================

    [Fact]
    public async Task Detect_AllEvidencesHaveFilePathAndReason()
    {
        // Arrange
        var fixturePath = Path.Combine(_fixturesRoot, "sample-csharp-api");
        var analysisId = Guid.NewGuid();

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert
        foreach (var evidence in result.Evidences)
        {
            Assert.False(string.IsNullOrWhiteSpace(evidence.FilePath), "Evidence FilePath must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(evidence.Reason), "Evidence Reason must not be empty");
            Assert.InRange(evidence.Layer, 1, 3);
        }
    }

    [Fact]
    public async Task Detect_NoDbContextRepo_DoesNotClaimDatabase()
    {
        // Arrange: Library fixture has no DbContext
        var fixturePath = Path.Combine(_fixturesRoot, "sample-library");
        var analysisId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        await SeedAnalysisData(analysisId, repoId, seedPublicClasses: true);

        // Act
        var result = await _detector.DetectAsync(analysisId, fixturePath);

        // Assert: No evidence claiming database
        Assert.DoesNotContain(result.Evidences, e => e.Reason.Contains("DbContext"));
        Assert.DoesNotContain(result.Evidences, e => e.Reason.Contains("Database entities"));
    }

    // ========================================================================================
    // Helpers: Seed Analysis Data
    // ========================================================================================

    private async Task SeedAnalysisData(
        Guid analysisId, Guid repoId,
        bool seedApiEndpoints = false,
        bool seedControllers = false,
        bool seedDbContext = false,
        bool seedEntryPoint = false,
        bool seedPublicClasses = false,
        bool seedFrontendRoutes = false)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "test-repo",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = "https://github.com/test/repo"
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
            Name = "TestProject",
            Path = "src/TestProject",
            Language = "C#",
            ProjectType = "CSharp"
        };
        _context.Projects.Add(project);

        var sourceFile = new SourceFile
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            ProjectId = project.Id,
            Path = "src/TestProject/TestFile.cs",
            Language = "C#",
            Size = 100,
            Hash = "abc123"
        };
        _context.SourceFiles.Add(sourceFile);

        if (seedApiEndpoints)
        {
            _context.ApiEndpoints.Add(new ApiEndpoint
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                ProjectId = project.Id,
                Method = "GET",
                Route = "/api/weather",
                Controller = "WeatherController",
                Action = "Get"
            });
        }

        if (seedControllers)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "WeatherController",
                FullName = "TestProject.Controllers.WeatherController",
                SymbolType = SymbolType.Class,
                StartLine = 1,
                EndLine = 20
            });
        }

        if (seedDbContext)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "AppDbContext",
                FullName = "TestProject.Data.AppDbContext",
                SymbolType = SymbolType.Class,
                StartLine = 1,
                EndLine = 15
            });

            _context.DatabaseEntities.Add(new DatabaseEntity
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                Name = "WeatherForecast",
                EntityType = "Table"
            });
        }

        if (seedEntryPoint)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "Program",
                FullName = "TestProject.Program",
                SymbolType = SymbolType.Class,
                StartLine = 1,
                EndLine = 10
            });
        }

        if (seedPublicClasses)
        {
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "Calculator",
                FullName = "MathLib.Calculator",
                SymbolType = SymbolType.Class,
                StartLine = 1,
                EndLine = 10
            });
            _context.CodeSymbols.Add(new CodeSymbol
            {
                Id = Guid.NewGuid(),
                SourceFileId = sourceFile.Id,
                Name = "IValidator",
                FullName = "MathLib.IValidator",
                SymbolType = SymbolType.Interface,
                StartLine = 12,
                EndLine = 16
            });
        }

        if (seedFrontendRoutes)
        {
            _context.SourceFiles.Add(new SourceFile
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                ProjectId = project.Id,
                Path = "app/page.tsx",
                Language = "TypeScript",
                Size = 200,
                Hash = "def456"
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedMonorepoAnalysisData(Guid analysisId, Guid repoId)
    {
        var repo = new Repository
        {
            Id = repoId,
            Name = "monorepo",
            SourceType = RepositorySourceType.GitUrl,
            SourceLocation = "https://github.com/test/monorepo"
        };
        _context.Repositories.Add(repo);

        var analysis = new Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed
        };
        _context.Analyses.Add(analysis);

        // Multiple projects
        var backendProject = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "Backend",
            Path = "Backend",
            Language = "C#",
            ProjectType = "CSharp"
        };
        var sharedProject = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "Shared",
            Path = "Shared",
            Language = "C#",
            ProjectType = "CSharp"
        };
        var frontendProject = new Project
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Name = "Frontend",
            Path = "Frontend",
            Language = "TypeScript",
            ProjectType = "Node"
        };

        _context.Projects.AddRange(backendProject, sharedProject, frontendProject);

        // Project references
        _context.Dependencies.Add(new Dependency
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            SourceId = backendProject.Id.ToString(),
            TargetId = sharedProject.Id.ToString(),
            DependencyType = DependencyType.ProjectReference
        });

        await _context.SaveChangesAsync();
    }
}
