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

    [Fact]
    public void Scanner_WhenTotalRepoSizeExceedsLimit_StopsTraversalAndRecordsScanError()
    {
        // Arrange: 3 files of 100 bytes each = 300 bytes, limit to 250 bytes
        File.WriteAllText(Path.Combine(_testDir, "File1.cs"), new string('a', 100));
        File.WriteAllText(Path.Combine(_testDir, "File2.cs"), new string('b', 100));
        File.WriteAllText(Path.Combine(_testDir, "File3.cs"), new string('c', 100));

        var limits = new AnalysisLimits
        {
            MaxTotalRepositorySizeBytes = 250
        };

        var scanner = new RepositoryScanner();

        // Act
        var result = scanner.Scan(_testDir, limits);

        // Assert
        Assert.True(result.SourceFiles.Count <= 2, $"Expected <= 2 files, got {result.SourceFiles.Count}");
        Assert.Contains(result.ScanErrors, err => err.Contains("Maximum repository total size limit reached"));
    }

    [Fact]
    public void Engine_WhenRelationshipsExceedLimit_CapsRelationshipsAndRecordsWarning()
    {
        // Arrange: Create a repository with a C# file that has multiple relationships
        var csharpDir = Path.Combine(_testDir, "src", "App");
        Directory.CreateDirectory(csharpDir);
        File.WriteAllText(Path.Combine(csharpDir, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var csharpCode = """
            namespace App;
            public interface IService1 {}
            public interface IService2 {}
            public interface IService3 {}
            public class Consumer : IService1, IService2, IService3 {}
            """;
        File.WriteAllText(Path.Combine(csharpDir, "Consumer.cs"), csharpCode);

        var limits = new AnalysisLimits
        {
            MaxRelationships = 2
        };

        var engine = new RepoLens.Analysis.Orchestration.RepositoryAnalysisEngine();

        // Act
        var result = engine.AnalyzeRepository(_testDir, limits: limits);

        // Assert: Graph relationships must be capped at 2 and warning recorded
        Assert.True(result.Analysis.Relationships.Count <= 2, $"Expected <= 2 relationships, got {result.Analysis.Relationships.Count}");
        Assert.Contains(result.AllErrors, err => err.Contains("Maximum graph relationships limit reached"));
    }
}
