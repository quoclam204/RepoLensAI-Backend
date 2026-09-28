using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Acquisition;
using Xunit;

namespace RepoLens.UnitTests.Acquisition;

public class ZipRepositorySourceTests : IDisposable
{
    private readonly string _tempWorkspaceDir;

    public ZipRepositorySourceTests()
    {
        _tempWorkspaceDir = Path.Combine(Path.GetTempPath(), "repolens-zip-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempWorkspaceDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempWorkspaceDir))
        {
            try { Directory.Delete(_tempWorkspaceDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class MockWorkspace : ITemporaryWorkspace
    {
        public MockWorkspace(string rootPath)
        {
            AnalysisId = Guid.NewGuid();
            RootPath = rootPath;
        }

        public Guid AnalysisId { get; }
        public string RootPath { get; }
        public bool Exists => Directory.Exists(RootPath);

        public Task CleanupAsync()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
            return Task.CompletedTask;
        }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AcquireAsync_WithValidZipArchive_ExtractsFilesSuccessfully()
    {
        using var zipStream = CreateZipStream(new Dictionary<string, string>
        {
            ["src/App.cs"] = "public class App { }",
            ["README.md"] = "# Hello World"
        });

        var options = Options.Create(new AcquisitionOptions());
        var source = new ZipRepositorySource(options, NullLogger<ZipRepositorySource>.Instance);
        var workspace = new MockWorkspace(_tempWorkspaceDir);

        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: zipStream,
            FileName: "test.zip",
            ContentLength: zipStream.Length);

        var result = await source.AcquireAsync(request, workspace);

        Assert.True(result.Success);
        Assert.Equal(2, result.FileCount);
        Assert.True(File.Exists(Path.Combine(_tempWorkspaceDir, "src", "App.cs")));
        Assert.True(File.Exists(Path.Combine(_tempWorkspaceDir, "README.md")));
        Assert.Equal("public class App { }", await File.ReadAllTextAsync(Path.Combine(_tempWorkspaceDir, "src", "App.cs")));
    }

    [Fact]
    public async Task AcquireAsync_WithPathTraversalZipSlip_RejectsExtraction()
    {
        using var zipStream = CreateZipStream(new Dictionary<string, string>
        {
            ["../../evil.txt"] = "malicious payload"
        });

        var options = Options.Create(new AcquisitionOptions());
        var source = new ZipRepositorySource(options, NullLogger<ZipRepositorySource>.Instance);
        var workspace = new MockWorkspace(_tempWorkspaceDir);

        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: zipStream,
            FileName: "evil.zip",
            ContentLength: zipStream.Length);

        var result = await source.AcquireAsync(request, workspace);

        Assert.False(result.Success);
        Assert.Contains("Path traversal attempt detected", result.ErrorMessage);
    }

    [Fact]
    public async Task AcquireAsync_ExceedingMaxUncompressedBytes_RejectsAsZipBomb()
    {
        using var zipStream = CreateZipStream(new Dictionary<string, string>
        {
            ["file1.txt"] = new string('A', 5000)
        });

        // Set maximum limit smaller than uncompressed content
        var options = Options.Create(new AcquisitionOptions
        {
            MaxUncompressedBytes = 1000
        });

        var source = new ZipRepositorySource(options, NullLogger<ZipRepositorySource>.Instance);
        var workspace = new MockWorkspace(_tempWorkspaceDir);

        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: zipStream,
            FileName: "bomb.zip",
            ContentLength: zipStream.Length);

        var result = await source.AcquireAsync(request, workspace);

        Assert.False(result.Success);
        Assert.Contains("Zip bomb defense triggered", result.ErrorMessage);
    }

    private static MemoryStream CreateZipStream(Dictionary<string, string> files)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}
