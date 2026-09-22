using RepoLens.Analysis.CSharp;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.CSharp;

public class CallGraphExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithOrderServiceFixture_ExtractsCallWithMediumConfidence()
    {
        // Arrange
        var code = """
            namespace Demo;

            public class OrderService
            {
                public Order GetOrder(int id)
                {
                    return repository.Get(id);
                }
            }
            """;

        var tree = _parser.ParseText(code, "OrderService.cs");

        // Act
        var calls = CallGraphExtractor.ExtractFromTree(tree, "OrderService.cs");

        // Assert
        Assert.Single(calls);
        var call = calls[0];
        Assert.Equal("OrderService.GetOrder", call.CallerSymbol);
        Assert.Equal("Get", call.CalleeName);
        Assert.Equal("repository.Get", call.FullInvocation);
        Assert.Equal(ConfidenceScore.Medium, call.Confidence);
        Assert.Equal("OrderService.cs", call.Location.FilePath);
        Assert.True(call.Location.StartLine > 0);
        Assert.Contains("repository.Get(id)", call.Snippet);
    }

    [Fact]
    public void ExtractFromTree_WithNestedInvocations_ExtractsAllInvocationLevels()
    {
        // Arrange
        var code = """
            namespace Demo;

            public class Pipeline
            {
                public void Execute()
                {
                    config.GetBuilder().Build();
                }
            }
            """;

        var tree = _parser.ParseText(code, "Pipeline.cs");

        // Act
        var calls = CallGraphExtractor.ExtractFromTree(tree, "Pipeline.cs");

        // Assert
        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, c => c.CallerSymbol == "Pipeline.Execute" && c.CalleeName == "Build");
        Assert.Contains(calls, c => c.CallerSymbol == "Pipeline.Execute" && c.CalleeName == "GetBuilder");
    }

    [Fact]
    public void ExtractFromTree_WithConstructorInvocation_CapturesConstructorCallerSymbol()
    {
        // Arrange
        var code = """
            namespace Demo;

            public class Service
            {
                public Service()
                {
                    Initialize();
                }

                private void Initialize() {}
            }
            """;

        var tree = _parser.ParseText(code, "Service.cs");

        // Act
        var calls = CallGraphExtractor.ExtractFromTree(tree, "Service.cs");

        // Assert
        Assert.Single(calls);
        Assert.Equal("Service..ctor", calls[0].CallerSymbol);
        Assert.Equal("Initialize", calls[0].CalleeName);
        Assert.Equal(ConfidenceScore.Medium, calls[0].Confidence);
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
