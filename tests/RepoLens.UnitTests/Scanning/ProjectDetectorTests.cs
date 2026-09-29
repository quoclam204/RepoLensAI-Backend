using RepoLens.Application.Models.Scanning;
using RepoLens.Infrastructure.Scanning;
using Xunit;

namespace RepoLens.UnitTests.Scanning;

public class ProjectDetectorTests : IDisposable
{
    private readonly ProjectDetector _detector = new();
    private readonly string _tempDir;

    public ProjectDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "repolens-proj-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void DetectProject_WithCsproj_ReturnsCSharpProject()
    {
        var relPath = "src/RepoLens.Domain/RepoLens.Domain.csproj";
        var fullPath = Path.Combine(_tempDir, "RepoLens.Domain.csproj");

        var project = _detector.DetectProject(relPath, fullPath);

        Assert.NotNull(project);
        Assert.Equal("RepoLens.Domain", project.Name);
        Assert.Equal(relPath, project.RelativePath);
        Assert.Equal(ScannedProjectType.CSharpProject, project.ProjectType);
    }

    [Fact]
    public void DetectProject_WithSln_ReturnsDotNetSolution()
    {
        var relPath = "RepoLens.sln";
        var fullPath = Path.Combine(_tempDir, "RepoLens.sln");

        var project = _detector.DetectProject(relPath, fullPath);

        Assert.NotNull(project);
        Assert.Equal("RepoLens", project.Name);
        Assert.Equal(ScannedProjectType.DotNetSolution, project.ProjectType);
    }

    [Fact]
    public async Task DetectProject_WithPackageJson_ReturnsNodeProjectWithName()
    {
        var packageJsonPath = Path.Combine(_tempDir, "package.json");
        await File.WriteAllTextAsync(packageJsonPath, """{"name": "my-awesome-app", "version": "1.0.0"}""");

        var relPath = "frontend/package.json";
        var project = _detector.DetectProject(relPath, packageJsonPath);

        Assert.NotNull(project);
        Assert.Equal("my-awesome-app", project.Name);
        Assert.Equal(ScannedProjectType.NodeProject, project.ProjectType);
    }

    [Fact]
    public void DetectProject_WithTsconfigJson_ReturnsTypeScriptProject()
    {
        var relPath = "frontend/tsconfig.json";
        var fullPath = Path.Combine(_tempDir, "tsconfig.json");

        var project = _detector.DetectProject(relPath, fullPath);

        Assert.NotNull(project);
        Assert.Equal(ScannedProjectType.TypeScriptProject, project.ProjectType);
    }

    [Fact]
    public void DetectProject_WithRegularSourceFile_ReturnsNull()
    {
        var relPath = "src/Program.cs";
        var fullPath = Path.Combine(_tempDir, "Program.cs");

        var project = _detector.DetectProject(relPath, fullPath);

        Assert.Null(project);
    }
}
