using RepoLens.Infrastructure.Scanning;
using Xunit;

namespace RepoLens.UnitTests.Scanning;

public class IgnoreRulesTests
{
    private readonly IgnoreRules _ignoreRules = new();

    [Theory]
    [InlineData(".git")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("node_modules")]
    [InlineData("dist")]
    [InlineData("build")]
    [InlineData(".vs")]
    [InlineData(".vscode")]
    [InlineData("coverage")]
    public void ShouldIgnoreDirectory_WithStandardIgnoredDirectories_ReturnsTrue(string directory)
    {
        Assert.True(_ignoreRules.ShouldIgnoreDirectory(directory));
    }

    [Theory]
    [InlineData("src")]
    [InlineData("Controllers")]
    [InlineData("Services")]
    [InlineData("components")]
    [InlineData("tests")]
    public void ShouldIgnoreDirectory_WithSourceDirectories_ReturnsFalse(string directory)
    {
        Assert.False(_ignoreRules.ShouldIgnoreDirectory(directory));
    }

    [Theory]
    [InlineData("RepoLens.dll")]
    [InlineData("app.exe")]
    [InlineData("symbols.pdb")]
    [InlineData("archive.zip")]
    [InlineData("image.png")]
    [InlineData("logo.svg")]
    [InlineData("bundle.min.js")]
    [InlineData("styles.min.css")]
    [InlineData("bundle.js.map")]
    public void ShouldIgnoreFile_WithIgnoredExtensionsAndArtifacts_ReturnsTrue(string fileName)
    {
        Assert.True(_ignoreRules.ShouldIgnoreFile(fileName));
    }

    [Theory]
    [InlineData("Repository.cs")]
    [InlineData("app.ts")]
    [InlineData("server.js")]
    [InlineData("package.json")]
    [InlineData("RepoLens.Domain.csproj")]
    [InlineData("README.md")]
    [InlineData("styles.css")]
    public void ShouldIgnoreFile_WithSourceFiles_ReturnsFalse(string fileName)
    {
        Assert.False(_ignoreRules.ShouldIgnoreFile(fileName));
    }
}
