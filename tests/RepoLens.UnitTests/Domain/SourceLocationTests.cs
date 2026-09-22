using RepoLens.Domain.Exceptions;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.UnitTests.Domain;

public class SourceLocationTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesInstanceAndNormalizesPath()
    {
        var location = new SourceLocation("src\\RepoLens.Api\\Program.cs", 10, 25);

        Assert.Equal("src/RepoLens.Api/Program.cs", location.FilePath);
        Assert.Equal(10, location.StartLine);
        Assert.Equal(25, location.EndLine);
        Assert.Equal("src/RepoLens.Api/Program.cs:10-25", location.ToString());
    }

    [Fact]
    public void Constructor_WithSameStartAndEndLine_Succeeds()
    {
        var location = new SourceLocation("src/file.cs", 42, 42);

        Assert.Equal(42, location.StartLine);
        Assert.Equal(42, location.EndLine);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithStartLineLessThanOne_ThrowsInvalidEvidenceRangeException(int startLine)
    {
        var ex = Assert.Throws<InvalidEvidenceRangeException>(() =>
            new SourceLocation("src/file.cs", startLine, 10));

        Assert.Equal(startLine, ex.StartLine);
    }

    [Fact]
    public void Constructor_WithEndLineLessThanStartLine_ThrowsInvalidEvidenceRangeException()
    {
        var ex = Assert.Throws<InvalidEvidenceRangeException>(() =>
            new SourceLocation("src/file.cs", 15, 14));

        Assert.Equal(15, ex.StartLine);
        Assert.Equal(14, ex.EndLine);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrWhitespaceFilePath_ThrowsDomainException(string? invalidPath)
    {
        Assert.Throws<DomainException>(() =>
            new SourceLocation(invalidPath!, 1, 10));
    }
}
