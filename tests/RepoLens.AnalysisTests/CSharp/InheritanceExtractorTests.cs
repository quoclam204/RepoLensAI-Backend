using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class InheritanceExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithBaseServiceAndIOrderService_DistinguishesInheritsAndImplementsWithEvidence()
    {
        // Arrange
        var code = """
            namespace Demo;

            public class BaseService {}
            public interface IOrderService {}

            public class OrderService : BaseService, IOrderService {}
            """;

        var tree = _parser.ParseText(code, "OrderService.cs");

        // Act
        var inheritances = InheritanceExtractor.ExtractFromTree(tree, "OrderService.cs");

        // Assert
        var orderServiceInheritances = inheritances.Where(i => i.DerivedType == "OrderService").ToList();
        Assert.Equal(2, orderServiceInheritances.Count);

        var baseService = orderServiceInheritances.Single(i => i.BaseType == "BaseService");
        Assert.False(baseService.IsInterfaceHeuristic);
        Assert.Equal("OrderService.cs", baseService.Location.FilePath);
        Assert.True(baseService.Location.StartLine > 0);
        Assert.Contains("BaseService", baseService.Snippet);

        var iOrderService = orderServiceInheritances.Single(i => i.BaseType == "IOrderService");
        Assert.True(iOrderService.IsInterfaceHeuristic);
        Assert.Equal("OrderService.cs", iOrderService.Location.FilePath);
        Assert.True(iOrderService.Location.StartLine > 0);
        Assert.Contains("IOrderService", iOrderService.Snippet);
    }

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
