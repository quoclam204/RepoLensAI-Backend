using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.UnitTests.Infrastructure.Storage;

/// <summary>
/// Unit tests for <see cref="TemporaryWorkspace"/> and <see cref="TemporaryWorkspaceManager"/> (T031).
/// Validates workspace creation, isolation, cleanup, and OS-specific edge cases.
/// </summary>
public class TemporaryWorkspaceTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly TemporaryWorkspaceManager _manager;

    public TemporaryWorkspaceTests()
    {
        // Use a unique temp directory for each test run to avoid collisions
        _testBaseDirectory = Path.Combine(
            Path.GetTempPath(),
            "repolens-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testBaseDirectory);

        var options = Options.Create(new WorkspaceOptions
        {
            BaseDirectory = _testBaseDirectory
        });

        var managerLogger = new LoggerFactory().CreateLogger<TemporaryWorkspaceManager>();
        var workspaceLogger = new LoggerFactory().CreateLogger<TemporaryWorkspace>();

        _manager = new TemporaryWorkspaceManager(options, managerLogger, workspaceLogger);
    }

    public void Dispose()
    {
        // Clean up test base directory after all tests complete
        if (Directory.Exists(_testBaseDirectory))
        {
            try
            {
                Directory.Delete(_testBaseDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup in test teardown
            }
        }
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ShouldCreateDirectoryWithAnalysisId()
    {
        // Arrange
        var analysisId = Guid.NewGuid();

        // Act
        var workspace = await _manager.CreateWorkspaceAsync(analysisId);

        // Assert
        Assert.NotNull(workspace);
        Assert.Equal(analysisId, workspace.AnalysisId);
        Assert.True(workspace.Exists);
        Assert.True(Directory.Exists(workspace.RootPath));
        Assert.Contains(analysisId.ToString("N"), workspace.RootPath);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_TwoAnalyses_ShouldBeIsolated()
    {
        // Arrange
        var analysisId1 = Guid.NewGuid();
        var analysisId2 = Guid.NewGuid();

        // Act
        var workspace1 = await _manager.CreateWorkspaceAsync(analysisId1);
        var workspace2 = await _manager.CreateWorkspaceAsync(analysisId2);

        // Create a test file in workspace1
        var testFile = Path.Combine(workspace1.RootPath, "test.txt");
        await File.WriteAllTextAsync(testFile, "workspace1-data");

        // Assert: workspaces are at different paths
        Assert.NotEqual(workspace1.RootPath, workspace2.RootPath);

        // Assert: file in workspace1 does not exist in workspace2
        Assert.True(File.Exists(testFile));
        Assert.False(File.Exists(Path.Combine(workspace2.RootPath, "test.txt")));
    }

    [Fact]
    public async Task CleanupAsync_ShouldRemoveAllFilesAndDirectories()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var workspace = await _manager.CreateWorkspaceAsync(analysisId);

        // Create nested structure
        var subDir = Path.Combine(workspace.RootPath, "src", "Models");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "README.md"), "# Test");
        await File.WriteAllTextAsync(Path.Combine(subDir, "User.cs"), "public class User {}");

        Assert.True(workspace.Exists);

        // Act
        await workspace.CleanupAsync();

        // Assert
        Assert.False(workspace.Exists);
        Assert.False(Directory.Exists(workspace.RootPath));
    }

    [Fact]
    public async Task CleanupAsync_ShouldHandleReadOnlyFiles()
    {
        // Arrange — simulates Windows behavior when Git clones files with ReadOnly attribute
        var analysisId = Guid.NewGuid();
        var workspace = await _manager.CreateWorkspaceAsync(analysisId);

        var gitObjectsDir = Path.Combine(workspace.RootPath, ".git", "objects", "pack");
        Directory.CreateDirectory(gitObjectsDir);

        var readOnlyFile = Path.Combine(gitObjectsDir, "pack-abc123.pack");
        await File.WriteAllTextAsync(readOnlyFile, "fake pack data");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var readOnlyFile2 = Path.Combine(gitObjectsDir, "pack-abc123.idx");
        await File.WriteAllTextAsync(readOnlyFile2, "fake index data");
        File.SetAttributes(readOnlyFile2, FileAttributes.ReadOnly);

        // Verify setup: files are indeed read-only
        Assert.True(File.GetAttributes(readOnlyFile).HasFlag(FileAttributes.ReadOnly));

        // Act — should NOT throw UnauthorizedAccessException
        await workspace.CleanupAsync();

        // Assert
        Assert.False(workspace.Exists);
        Assert.False(File.Exists(readOnlyFile));
    }

    [Fact]
    public async Task CleanupAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var workspace = await _manager.CreateWorkspaceAsync(analysisId);
        await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "temp.txt"), "data");

        // Act & Assert — calling cleanup multiple times should be safe
        await workspace.CleanupAsync();
        await workspace.CleanupAsync(); // second call should be a no-op
        Assert.False(workspace.Exists);
    }

    [Fact]
    public async Task DisposeAsync_ShouldCleanupWorkspace()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        string rootPath;

        // Act
        await using (var workspace = await _manager.CreateWorkspaceAsync(analysisId))
        {
            rootPath = workspace.RootPath;
            await File.WriteAllTextAsync(Path.Combine(rootPath, "data.json"), "{}");
            Assert.True(Directory.Exists(rootPath));
        }
        // workspace is disposed here

        // Assert
        Assert.False(Directory.Exists(rootPath));
    }

    [Fact]
    public async Task GetWorkspaceAsync_ExistingWorkspace_ShouldReturnIt()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        await _manager.CreateWorkspaceAsync(analysisId);

        // Act
        var retrieved = await _manager.GetWorkspaceAsync(analysisId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(analysisId, retrieved.AnalysisId);
        Assert.True(retrieved.Exists);
    }

    [Fact]
    public async Task GetWorkspaceAsync_NonExistentWorkspace_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _manager.GetWorkspaceAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CleanupWorkspaceAsync_ShouldRemoveWorkspaceViaManager()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var workspace = await _manager.CreateWorkspaceAsync(analysisId);
        var rootPath = workspace.RootPath;
        await File.WriteAllTextAsync(Path.Combine(rootPath, "file.cs"), "class A {}");

        // Act
        await _manager.CleanupWorkspaceAsync(analysisId);

        // Assert
        Assert.False(Directory.Exists(rootPath));
        var retrieved = await _manager.GetWorkspaceAsync(analysisId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task CleanupWorkspaceAsync_NonExistentAnalysis_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert — should be safe to call
        await _manager.CleanupWorkspaceAsync(nonExistentId);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ExistingDirectory_ShouldCleanAndRecreate()
    {
        // Arrange — create workspace, add files, then create again
        var analysisId = Guid.NewGuid();
        var workspace1 = await _manager.CreateWorkspaceAsync(analysisId);
        await File.WriteAllTextAsync(Path.Combine(workspace1.RootPath, "old-file.txt"), "old data");

        // Act — re-create should clean up old content
        var workspace2 = await _manager.CreateWorkspaceAsync(analysisId);

        // Assert
        Assert.True(workspace2.Exists);
        Assert.False(File.Exists(Path.Combine(workspace2.RootPath, "old-file.txt")));
    }
}
