using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Analysis.Evidence;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.Evidence;

public class EvidenceFactoryTests
{
    [Fact]
    public void Create_WithValidArguments_ReturnsConfiguredEvidence()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var evidence = EvidenceFactory.Create(
            analysisJobId: jobId,
            filePath: "src/Controllers/UserController.cs",
            startLine: 10,
            endLine: 15,
            snippet: "[HttpGet] public IActionResult Get() => Ok();",
            evidenceType: EvidenceType.Route,
            confidence: ConfidenceScore.Exact,
            symbol: "UserController.Get");

        // Assert
        Assert.Equal(jobId, evidence.AnalysisJobId);
        Assert.Equal("src/Controllers/UserController.cs", evidence.Location.FilePath);
        Assert.Equal(10, evidence.Location.StartLine);
        Assert.Equal(15, evidence.Location.EndLine);
        Assert.NotNull(evidence.Confidence);
        Assert.Equal(1.0f, evidence.Confidence.Value.Value);
        Assert.Equal("UserController.Get", evidence.Symbol);
    }

    [Fact]
    public void CreateFromSyntaxNode_WithParsedNode_ExtractsAccurateLocationAndSnippet()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tree = CSharpSyntaxTree.ParseText("public class Sample { public void Run() {} }");
        var classDeclaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        // Act
        var evidence = EvidenceFactory.CreateFromSyntaxNode(
            analysisJobId: jobId,
            filePath: "Sample.cs",
            node: classDeclaration,
            evidenceType: EvidenceType.Declaration,
            confidence: ConfidenceScore.High,
            symbol: "Sample");

        // Assert
        Assert.Equal(jobId, evidence.AnalysisJobId);
        Assert.Equal("Sample.cs", evidence.Location.FilePath);
        Assert.Equal(1, evidence.Location.StartLine);
        Assert.Equal(EvidenceType.Declaration, evidence.EvidenceType);
        Assert.Equal("Sample", evidence.Symbol);
    }

    [Fact]
    public void SanitizeSnippet_WithWhitespaceOrNull_ReturnsPlaceholder()
    {
        // Arrange & Act (Negative / edge case)
        var resultNull = EvidenceFactory.SanitizeSnippet(null);
        var resultWhitespace = EvidenceFactory.SanitizeSnippet("   \t  \n");

        // Assert
        Assert.Equal("[No snippet available]", resultNull);
        Assert.Equal("[No snippet available]", resultWhitespace);
    }
}
