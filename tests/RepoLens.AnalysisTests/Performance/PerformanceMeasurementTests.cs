using System.Diagnostics;
using RepoLens.Analysis.Orchestration;
using RepoLens.Analysis.Scanning;

namespace RepoLens.AnalysisTests.Performance;

/// <summary>
/// Performance measurement tests for repository scanning and static analysis (T108, T109, NFR-PERF-001).
/// </summary>
public class PerformanceMeasurementTests : IDisposable
{
    private readonly string _testDir;

    public PerformanceMeasurementTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RepoLens_Perf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        GenerateSyntheticRepository(_testDir, fileCount: 50);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void T108_MeasureRepositoryScanPerformance_CompletesWithinSla()
    {
        // Arrange
        var scanner = new RepositoryScanner();
        var sw = Stopwatch.StartNew();

        // Act
        var result = scanner.Scan(_testDir);
        sw.Stop();

        // Assert
        Assert.True(result.SourceFiles.Count >= 50, "Scanner must discover all synthetic files");
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Scan should take < 2000ms, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void T109_MeasureStaticAnalysisPerformance_CompletesWithinSla()
    {
        // Arrange
        var engine = new RepositoryAnalysisEngine();
        var sw = Stopwatch.StartNew();

        // Act
        var result = engine.AnalyzeRepository(_testDir);
        sw.Stop();

        // Assert
        Assert.NotEmpty(result.Analysis.Nodes);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Static analysis should take < 5000ms, took {sw.ElapsedMilliseconds}ms");
    }

    private static void GenerateSyntheticRepository(string rootDir, int fileCount)
    {
        File.WriteAllText(Path.Combine(rootDir, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        for (var i = 0; i < fileCount; i++)
        {
            var content = $$"""
            namespace PerfApp.Module{{i % 5}};

            public interface IService{{i}}
            {
                void Execute{{i}}();
            }

            public class Service{{i}} : IService{{i}}
            {
                public void Execute{{i}}() { }
            }
            """;
            File.WriteAllText(Path.Combine(rootDir, $"Service{i}.cs"), content);
        }
    }
}
