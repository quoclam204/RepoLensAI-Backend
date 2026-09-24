using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class CSharpFileParserTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ParseText_WithValidCSharp_ReturnsSyntaxTreeAndCompilationUnit()
    {
        // Arrange
        var code = "namespace TestApp; public class Sample { public void DoSomething() {} }";

        // Act
        var tree = _parser.ParseText(code, "Sample.cs");
        var compilationUnit = _parser.GetCompilationUnit(tree);

        // Assert
        Assert.NotNull(tree);
        Assert.NotNull(compilationUnit);
        Assert.DoesNotContain(tree.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void ParseText_WithEmptyCode_ReturnsEmptyCompilationUnitWithoutMembers()
    {
        // Arrange (Negative case)
        var code = "   // only whitespace and comments\n";

        // Act
        var tree = _parser.ParseText(code, "Empty.cs");
        var compilationUnit = _parser.GetCompilationUnit(tree);

        // Assert
        Assert.NotNull(tree);
        Assert.Empty(compilationUnit.Members);
    }

    [Fact]
    public async Task ParseFileAsync_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange (Negative case)
        var nonExistentPath = "D:/Invalid/NonExistent/File.cs";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _parser.ParseFileAsync(nonExistentPath));
    }
}
