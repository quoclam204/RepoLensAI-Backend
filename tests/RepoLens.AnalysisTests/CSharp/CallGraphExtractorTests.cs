using RepoLens.Analysis.CSharp;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.CSharp;

public class CallGraphExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithMethodInvocation_ExtractsCallWithMediumConfidence()
    {
        // Arrange
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void DoWork()
                {
                    LogMessage("started");
                }

                private void LogMessage(string msg) {}
            }
            """;

        var tree = _parser.ParseText(code, "Service.cs");

        // Act
        var calls = CallGraphExtractor.ExtractFromTree(tree, "Service.cs");

        // Assert
        Assert.Single(calls);
        var call = calls[0];
        Assert.Equal("Service.DoWork", call.CallerSymbol);
        Assert.Equal("LogMessage", call.CalleeName);
        Assert.Equal(ConfidenceScore.Medium, call.Confidence);
    }

    [Fact]
    public void ExtractFromTree_WhenNoMethodCallsExist_ReturnsEmptyCallsList()
    {
        // Arrange (Negative case)
        var code = """
            namespace MyNamespace;

            public class Calculator
            {
                public int Add(int a, int b)
                {
                    var result = a + b;
                    return result;
                }
            }
            """;

        var tree = _parser.ParseText(code, "Calculator.cs");

        // Act
        var calls = CallGraphExtractor.ExtractFromTree(tree, "Calculator.cs");

        // Assert
        Assert.Empty(calls);
    }
}
