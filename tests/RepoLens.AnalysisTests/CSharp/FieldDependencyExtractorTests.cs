using RepoLens.Analysis.CSharp;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.CSharp;

public class FieldDependencyExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithOrderServiceFixture_ExtractsInterfaceDependencyNotFieldName()
    {
        // Arrange
        var code = """
            namespace Demo;

            public class OrderService
            {
                private readonly IOrderRepository _repository;

                public OrderService(IOrderRepository repository)
                {
                    _repository = repository;
                }
            }
            """;

        var tree = _parser.ParseText(code, "OrderService.cs");

        // Act
        var dependencies = FieldDependencyExtractor.ExtractFromTree(tree, "OrderService.cs");

        // Assert
        Assert.NotEmpty(dependencies);

        // Verify target is the interface IOrderRepository, never the field/variable name _repository or repository
        Assert.All(dependencies, d =>
        {
            Assert.Equal("OrderService", d.SourceClass);
            Assert.Equal("IOrderRepository", d.TargetType);
            Assert.NotEqual("_repository", d.TargetType);
            Assert.NotEqual("repository", d.TargetType);
            Assert.Equal(ConfidenceScore.Medium, d.Confidence);
            Assert.Equal("OrderService.cs", d.Location.FilePath);
            Assert.True(d.Location.StartLine > 0);
            Assert.False(string.IsNullOrWhiteSpace(d.Snippet));
        });

        Assert.Contains(dependencies, d => d.Kind == FieldDependencyKind.Field);
        Assert.Contains(dependencies, d => d.Kind == FieldDependencyKind.ConstructorParameter);
    }

    [Fact]
    public void ExtractFromTree_WithGenericDependencies_HandlesWrappersAndGenericInterfaces()
    {
        // Arrange
        var code = """
            namespace Demo;

            public class Processor
            {
                private readonly IEnumerable<IOrderService> _services;
                private readonly IRepository<Order> _repository;

                public Processor(IEnumerable<IOrderService> services, IRepository<Order> repository)
                {
                    _services = services;
                    _repository = repository;
                }
            }
            """;

        var tree = _parser.ParseText(code, "Processor.cs");

        // Act
        var dependencies = FieldDependencyExtractor.ExtractFromTree(tree, "Processor.cs");

        // Assert
        // IEnumerable unwraps to IOrderService
        Assert.Contains(dependencies, d => d.TargetType == "IOrderService");
        // IRepository<Order> targets IRepository (the generic service contract)
        Assert.Contains(dependencies, d => d.TargetType == "IRepository");
    }

    [Fact]
    public void ExtractFromTree_WithFieldConstructorAndGenericCollection_ExtractsExpectedDependencies()
    {
        // Arrange
        var code = """
            namespace MyNamespace;

            public class ContractService
            {
                private readonly ContractRepository _repository;
                private readonly IEnumerable<IContractValidator> _validators;

                public ContractService(ContractRepository repository, IAuditLogger logger)
                {
                    _repository = repository;
                }
            }
            """;

        var tree = _parser.ParseText(code, "ContractService.cs");

        // Act
        var dependencies = FieldDependencyExtractor.ExtractFromTree(tree, "ContractService.cs");

        // Assert
        Assert.Contains(dependencies, d => d.SourceClass == "ContractService" &&
                                           d.TargetType == "ContractRepository" &&
                                           d.Kind == FieldDependencyKind.Field &&
                                           d.Confidence == ConfidenceScore.Medium);

        Assert.Contains(dependencies, d => d.SourceClass == "ContractService" &&
                                           d.TargetType == "IContractValidator" &&
                                           d.Kind == FieldDependencyKind.Field &&
                                           d.Confidence == ConfidenceScore.Medium);

        Assert.Contains(dependencies, d => d.SourceClass == "ContractService" &&
                                           d.TargetType == "IAuditLogger" &&
                                           d.Kind == FieldDependencyKind.ConstructorParameter &&
                                           d.Confidence == ConfidenceScore.Medium);
    }

    [Fact]
    public void ExtractFromTree_WithPrimitivesAndFrameworkTypes_ReturnsEmptyDependencies()
    {
        // Arrange (Negative Case 1)
        var code = """
            namespace MyNamespace;

            public class PrimitiveHolder
            {
                private int _count;
                private string _name;
                private bool _isActive;
                private Guid _id;
                private DateTime _createdAt;
                private CancellationToken _token;
                private List<string> _tags;

                public PrimitiveHolder(int count, string name, CancellationToken token)
                {
                    _count = count;
                }
            }
            """;

        var tree = _parser.ParseText(code, "PrimitiveHolder.cs");

        // Act
        var dependencies = FieldDependencyExtractor.ExtractFromTree(tree, "PrimitiveHolder.cs");

        // Assert
        Assert.Empty(dependencies);
    }

    [Fact]
    public void ExtractFromTree_WithEnumSuffixTypes_IgnoresEnumDependencies()
    {
        // Arrange (Negative Case 2: Status, Type, Stage, Kind, Mode heuristics)
        var code = """
            namespace MyNamespace;

            public class StatusTrackingService
            {
                private readonly AnalysisStage _currentStage;
                private readonly RepositoryStatus _status;
                private readonly EvidenceType _evidenceType;

                public StatusTrackingService(AnalysisStage stage, RepositoryStatus status)
                {
                    _currentStage = stage;
                    _status = status;
                }
            }
            """;

        var tree = _parser.ParseText(code, "StatusTrackingService.cs");

        // Act
        var dependencies = FieldDependencyExtractor.ExtractFromTree(tree, "StatusTrackingService.cs");

        // Assert
        Assert.Empty(dependencies);
    }
}
