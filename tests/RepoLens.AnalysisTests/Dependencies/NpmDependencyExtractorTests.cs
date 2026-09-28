using RepoLens.Analysis.Dependencies;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.Dependencies;

public class NpmDependencyExtractorTests
{
    private readonly NpmDependencyExtractor _extractor = new();

    [Fact]
    public void Analyze_WithValidPackageJson_ExtractsDependenciesAndDevDependencies()
    {
        // Arrange
        var json = """
        {
          "name": "my-awesome-app",
          "version": "1.0.0",
          "dependencies": {
            "react": "^18.2.0",
            "axios": "^1.6.0"
          },
          "devDependencies": {
            "typescript": "^5.3.0",
            "vite": "^5.0.0"
          }
        }
        """;

        // Act
        var result = _extractor.Analyze("package.json", json);

        // Assert
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Dependencies.Count);

        var react = result.Dependencies.First(d => d.PackageName == "react");
        Assert.Equal("^18.2.0", react.Version);
        Assert.Equal("package.json", react.Location.FilePath);
        Assert.True(react.Location.StartLine > 0);
        Assert.Equal(ConfidenceScore.High, react.Confidence);

        var ts = result.Dependencies.First(d => d.PackageName == "typescript");
        Assert.Equal("^5.3.0", ts.Version);

        var axios = result.Dependencies.First(d => d.PackageName == "axios");
        Assert.Equal("^1.6.0", axios.Version);

        var vite = result.Dependencies.First(d => d.PackageName == "vite");
        Assert.Equal("^5.0.0", vite.Version);
    }

    [Fact]
    public void Analyze_WhenPackageJsonHasNoDependencies_ReturnsEmptyListWithoutError()
    {
        // Arrange
        var json = """
        {
          "name": "empty-package",
          "version": "0.0.1"
        }
        """;

        // Act
        var result = _extractor.Analyze("package.json", json);

        // Assert
        Assert.Empty(result.Errors);
        Assert.Empty(result.Dependencies);
    }

    [Fact]
    public void Analyze_WhenJsonIsMalformed_ReturnsErrorGracefully()
    {
        // Arrange
        var malformedJson = "{ name: not valid json }";

        // Act
        var result = _extractor.Analyze("package.json", malformedJson);

        // Assert
        Assert.Empty(result.Dependencies);
        Assert.Single(result.Errors);
        Assert.Contains("Malformed package.json", result.Errors[0]);
    }

    [Fact]
    public void Analyze_WhenContentIsEmptyOrWhitespace_ReturnsEmptyResult()
    {
        // Act
        var result = _extractor.Analyze("package.json", "   ");

        // Assert
        Assert.Empty(result.Dependencies);
        Assert.Empty(result.Errors);
    }
}
