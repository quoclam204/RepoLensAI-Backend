using RepoLens.Infrastructure.Scanning;
using Xunit;

namespace RepoLens.UnitTests.Scanning;

public class LanguageDetectorTests
{
    private readonly LanguageDetector _detector = new();

    [Theory]
    [InlineData("src/Domain/Entity.cs", "C#")]
    [InlineData("components/Button.tsx", "TypeScript")]
    [InlineData("lib/utils.ts", "TypeScript")]
    [InlineData("index.js", "JavaScript")]
    [InlineData("server.cjs", "JavaScript")]
    [InlineData("package.json", "JSON")]
    [InlineData("RepoLens.Domain.csproj", "XML")]
    [InlineData("schema.sql", "SQL")]
    [InlineData("README.md", "Markdown")]
    [InlineData("styles.css", "CSS")]
    [InlineData("Dockerfile", "Dockerfile")]
    [InlineData("script.unknownext", "Unknown")]
    public void DetectLanguage_WithVariousFilePaths_ReturnsExpectedLanguage(string filePath, string expectedLanguage)
    {
        var language = _detector.DetectLanguage(filePath);

        Assert.Equal(expectedLanguage, language);
    }
}
