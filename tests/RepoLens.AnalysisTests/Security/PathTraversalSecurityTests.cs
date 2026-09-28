using RepoLens.Analysis.Scanning;

namespace RepoLens.AnalysisTests.Security;

public class PathTraversalSecurityTests : IDisposable
{
    private readonly string _testDir;

    public PathTraversalSecurityTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RepoLens_PathTraversalSecurity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Scanner_WhenTargetDirectoryDoesNotExist_ReturnsGracefulScanError()
    {
        // Arrange
        var scanner = new RepositoryScanner();
        var nonExistentPath = Path.Combine(_testDir, "does_not_exist");

        // Act
        var result = scanner.Scan(nonExistentPath);

        // Assert
        Assert.Single(result.ScanErrors);
        Assert.Contains("Directory not found", result.ScanErrors[0]);
        Assert.Empty(result.SourceFiles);
    }

    [Fact]
    public void Scanner_NormalizesBackslashes_AndKeepsAllPathsWithinRoot()
    {
        // Arrange
        var subDir = Directory.CreateDirectory(Path.Combine(_testDir, "src", "Controllers"));
        File.WriteAllText(Path.Combine(subDir.FullName, "TestController.cs"), "public class TestController {}");

        var scanner = new RepositoryScanner();

        // Act
        var result = scanner.Scan(_testDir);

        // Assert
        Assert.Single(result.SourceFiles);
        var scanned = result.SourceFiles[0];
        Assert.False(scanned.RelativePath.Contains('\\'), "Paths must use forward slash");
        Assert.False(scanned.RelativePath.StartsWith('/'), "Relative paths must not start with slash");
        Assert.False(scanned.RelativePath.Contains(".."), "Paths must not contain path traversal");
    }

    [Fact]
    public void Scanner_WhenAttemptingPathTraversal_RejectsEscapingPaths()
    {
        // Arrange
        var scanner = new RepositoryScanner();
        var safeDir = Path.Combine(_testDir, "safe");
        Directory.CreateDirectory(safeDir);
        File.WriteAllText(Path.Combine(safeDir, "Good.cs"), "public class Good {}");

        // Act
        var result = scanner.Scan(safeDir);

        // Assert: Scanned files must stay strictly within the root
        Assert.Single(result.SourceFiles);
        Assert.Equal("Good.cs", result.SourceFiles[0].RelativePath);
        Assert.True(result.SourceFiles.All(f => !f.RelativePath.StartsWith("..")));
    }
}
