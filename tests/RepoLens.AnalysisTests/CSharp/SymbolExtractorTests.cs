using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class SymbolExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithVariousDeclarations_ExtractsExpectedSymbols()
    {
        // Arrange
        var code = """
            namespace MyNamespace;

            public interface IMyService
            {
                string Process(int id);
            }

            public class MyService : IMyService
            {
                public int Count { get; set; }
                public string Process(int id) => id.ToString();
            }

            public record CustomerDto(string Name);
            public struct Coordinate { public double X; public double Y; }
            """;

        var tree = _parser.ParseText(code, "Service.cs");

        // Act
        var symbols = SymbolExtractor.ExtractFromTree(tree, "Service.cs");

        // Assert
        Assert.Contains(symbols, s => s.Kind == CSharpSymbolKind.Interface && s.Name == "IMyService");
        Assert.Contains(symbols, s => s.Kind == CSharpSymbolKind.Class && s.Name == "MyService");
        Assert.Contains(symbols, s => s.Kind == CSharpSymbolKind.Property && s.Name == "Count");
        Assert.Contains(symbols, s => s.Kind == CSharpSymbolKind.Method && s.Name == "Process");
        Assert.Contains(symbols, s => s.Kind == CSharpSymbolKind.Record && s.Name == "CustomerDto");
        Assert.Contains(symbols, s => s.Kind == CSharpSymbolKind.Struct && s.Name == "Coordinate");
    }

    [Fact]
    public void ExtractFromTree_WhenNoTypeOrMemberDeclarationsExist_ReturnsEmptySymbolList()
    {
        // Arrange (Negative case)
        var code = """
            // This file only contains comments and using statements
            using System;
            using System.Collections.Generic;
            """;

        var tree = _parser.ParseText(code, "EmptyUsings.cs");

        // Act
        var symbols = SymbolExtractor.ExtractFromTree(tree, "EmptyUsings.cs");

        // Assert
        Assert.Empty(symbols);
    }
}
