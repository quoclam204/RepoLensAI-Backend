using RepoLens.Analysis.Scanning;

namespace RepoLens.AnalysisTests.Performance;

public class LargeRepositoryLimitTests : IDisposable
{
    private readonly string _testDir;

    public LargeRepositoryLimitTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RepoLens_LargeRepoLimits_" + Guid.NewGuid().ToString("N"));
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
    public void Scanner_WhenFileCountExceedsMaxFilesLimit_StopsTraversalAndRecordsScanError()
    {
        // Arrange: create 10 files, limit to 5
        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(Path.Combine(_testDir, $"File_{i:D2}.cs"), $"public class File_{i:D2} {{}}");
        }

        var limits = new AnalysisLimits
        {
            MaxFiles = 5
        };

        var scanner = new RepositoryScanner();

        // Act
        var result = scanner.Scan(_testDir, limits);

        // Assert
        Assert.True(result.SourceFiles.Count <= 5, $"Expected <= 5 files, got {result.SourceFiles.Count}");
        Assert.Contains(result.ScanErrors, err => err.Contains("Maximum file count limit reached"));
    }

    [Fact]
    public void Scanner_WhenFileExceedsMaxFileSizeBytes_SkipsFileAndRecordsWarning()
    {
        // Arrange: create 1 small file, 1 file of 200 bytes, limit size to 100 bytes
        File.WriteAllText(Path.Combine(_testDir, "Small.cs"), "public class Small {}");
        File.WriteAllText(Path.Combine(_testDir, "Large.cs"), new string('x', 500));

        var limits = new AnalysisLimits
        {
            MaxFileSizeBytes = 200
        };

        var scanner = new RepositoryScanner();

        // Act
        var result = scanner.Scan(_testDir, limits);

        // Assert
        Assert.Single(result.SourceFiles);
        Assert.Equal("Small.cs", result.SourceFiles[0].RelativePath);
        Assert.Contains(result.ScanErrors, err => err.Contains("Skipping excessively large file 'Large.cs'"));
    }

    [Fact]
    public void Scanner_WhenCancelled_HandlesCancellationGracefully()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_testDir, $"File_{i}.cs"), "class A {}");
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var scanner = new RepositoryScanner();

        // Act
        var result = scanner.Scan(_testDir, cancellationToken: cts.Token);

        // Assert
        Assert.Contains(result.ScanErrors, err => err.Contains("cancelled"));
    }
}
