using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoLens.Application.Models.Scanning;
using RepoLens.Infrastructure.Scanning;
using Xunit;

namespace RepoLens.UnitTests.Scanning;

public class FileScannerTests : IDisposable
{
    private readonly string _workspaceDir;

    public FileScannerTests()
    {
        _workspaceDir = Path.Combine(Path.GetTempPath(), "repolens-scanner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_workspaceDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceDir))
        {
            try { Directory.Delete(_workspaceDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task ScanAsync_WithFullWorkspaceStructure_ScansAccuratelyAndAppliesAllRules()
    {
        // 1. Arrange: Setup realistic repository file tree
        var srcDir = Path.Combine(_workspaceDir, "src");
        var binDir = Path.Combine(_workspaceDir, "bin");
        var nodeModulesDir = Path.Combine(_workspaceDir, "node_modules");
        var frontendDir = Path.Combine(_workspaceDir, "frontend");

        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(nodeModulesDir);
        Directory.CreateDirectory(frontendDir);

        // Source files
        await File.WriteAllTextAsync(Path.Combine(srcDir, "App.cs"), "public class App { }");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(Path.Combine(frontendDir, "index.ts"), "export const hello = 'world';");
        await File.WriteAllTextAsync(Path.Combine(frontendDir, "package.json"), "{\"name\": \"my-web\"}");

        // Secrets to exclude
        await File.WriteAllTextAsync(Path.Combine(srcDir, ".env"), "DATABASE_URL=postgres://root:secret@localhost/db");

        // Ignored directories / binaries to skip
        await File.WriteAllTextAsync(Path.Combine(binDir, "App.dll"), "binary content");
        await File.WriteAllTextAsync(Path.Combine(nodeModulesDir, "package.json"), "{}");

        var scanner = new FileScanner(
            Options.Create(new ScanningOptions()),
            new IgnoreRules(),
            new SecretDetector(),
            new LanguageDetector(),
            new ProjectDetector(),
            NullLogger<FileScanner>.Instance);

        var analysisId = Guid.NewGuid();

        // 2. Act
        var result = await scanner.ScanAsync(analysisId, _workspaceDir);

        // 3. Assert
        Assert.NotNull(result);
        Assert.Equal(analysisId, result.AnalysisId);

        // Files count: App.cs, App.csproj, index.ts, package.json = 4 files (no .env, no bin/App.dll, no node_modules)
        Assert.Equal(4, result.TotalFiles);

        // Verify .env was filtered
        Assert.DoesNotContain(result.Files, f => f.RelativePath.EndsWith(".env"));

        // Verify bin and node_modules files were skipped
        Assert.DoesNotContain(result.Files, f => f.RelativePath.Contains("bin/"));
        Assert.DoesNotContain(result.Files, f => f.RelativePath.Contains("node_modules/"));

        // Verify path normalization uses forward slashes
        Assert.All(result.Files, f => Assert.DoesNotContain("\\", f.RelativePath));

        // Verify SHA-256 hash format (64-character lowercase hex)
        Assert.All(result.Files, f =>
        {
            Assert.Equal(64, f.Hash.Length);
            Assert.Matches("^[0-9a-f]{64}$", f.Hash);
        });

        // Verify detected languages
        Assert.Contains("C#", result.DetectedLanguages);
        Assert.Contains("TypeScript", result.DetectedLanguages);
        Assert.Contains("XML", result.DetectedLanguages);
        Assert.Contains("JSON", result.DetectedLanguages);

        // Verify detected projects
        Assert.Equal(2, result.DetectedProjects.Count);
        Assert.Contains(result.DetectedProjects, p => p.ProjectType == ScannedProjectType.CSharpProject && p.Name == "App");
        Assert.Contains(result.DetectedProjects, p => p.ProjectType == ScannedProjectType.NodeProject && p.Name == "my-web");
    }
}
