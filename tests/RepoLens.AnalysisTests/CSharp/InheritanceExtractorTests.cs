using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class InheritanceExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithBaseClassAndMultipleInterfaces_ExtractsAllRelationshipsCorrectly()
    {
        // Arrange
        var code = """
            namespace MyNamespace;

            public class BaseFoo {}
            public interface IBar {}
            public interface IBaz {}

            public class Foo : BaseFoo, IBar, IBaz {}
            """;

        var tree = _parser.ParseText(code, "Foo.cs");

        // Act
        var inheritances = InheritanceExtractor.ExtractFromTree(tree, "Foo.cs");

        // Assert
        var fooInheritances = inheritances.Where(i => i.DerivedType == "Foo").ToList();
        Assert.Equal(3, fooInheritances.Count);

        var baseClass = fooInheritances.Single(i => i.BaseType == "BaseFoo");
        Assert.False(baseClass.IsInterfaceHeuristic);

        var firstInterface = fooInheritances.Single(i => i.BaseType == "IBar");
        Assert.True(firstInterface.IsInterfaceHeuristic);

        var secondInterface = fooInheritances.Single(i => i.BaseType == "IBaz");
        Assert.True(secondInterface.IsInterfaceHeuristic);
    }

    [Fact]
    public void ExtractFromTree_WhenClassHasNoBaseList_ReturnsEmptyInheritances()
    {
        // Arrange (Negative case)
        var code = """
            namespace MyNamespace;

            public class StandaloneClass
            {
                public void DoWork() {}
            }

            public struct StandaloneStruct
            {
                public int Value;
            }
            """;

        var tree = _parser.ParseText(code, "Standalone.cs");

        // Act
        var inheritances = InheritanceExtractor.ExtractFromTree(tree, "Standalone.cs");

        // Assert
        Assert.Empty(inheritances);
    }
}
