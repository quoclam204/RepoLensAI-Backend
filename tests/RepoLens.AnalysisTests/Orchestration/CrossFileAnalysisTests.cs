using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.Orchestration;

public class CrossFileAnalysisTests
{
    private readonly ProjectAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_WithCrossFileInterfaceAndImplementation_ResolvesRelationshipWithEvidence()
    {
        // Arrange
        var interfaceFile = ("src/Contracts/IOrderService.cs", """
            namespace Demo.Contracts;

            public interface IOrderService
            {
                void ProcessOrder(int orderId);
            }
            """);

        var implementationFile = ("src/Services/OrderService.cs", """
            using Demo.Contracts;

            namespace Demo.Services;

            public class OrderService : IOrderService
            {
                public void ProcessOrder(int orderId) {}
            }
            """);

        var files = new[] { interfaceFile, implementationFile };

        // Act
        var result = _analyzer.Analyze(files, []);

        // Assert - Nodes exist
        var serviceNode = result.Nodes.FirstOrDefault(n => n.Id == "class:OrderService");
        var interfaceNode = result.Nodes.FirstOrDefault(n => n.Id == "interface:IOrderService");
        Assert.NotNull(serviceNode);
        Assert.NotNull(interfaceNode);

        // Assert - Cross-file Implements relationship
        var implRel = result.Relationships.FirstOrDefault(r =>
            r.SourceId == "class:OrderService" &&
            r.TargetId == "interface:IOrderService" &&
            r.Type == KnowledgeRelationshipType.Implements);

        Assert.NotNull(implRel);

        // Assert - Evidence-First Principle
        Assert.NotNull(implRel.Evidence);
        Assert.Equal("src/Services/OrderService.cs", implRel.Evidence.Location.FilePath);
        Assert.True(implRel.Evidence.Location.StartLine > 0);
        Assert.True(implRel.Evidence.Location.EndLine >= implRel.Evidence.Location.StartLine);
        Assert.Contains("IOrderService", implRel.Evidence.Snippet);
        Assert.Equal(ConfidenceScore.High, implRel.Evidence.Confidence);
        Assert.Equal(EvidenceType.Declaration, implRel.Evidence.EvidenceType);
    }

    [Fact]
    public void Analyze_EveryRelationship_AdheresToEvidenceFirstPrincipleWithValidConfidence()
    {
        // Arrange: A comprehensive set of files
        var domainFile = ("src/Domain/Order.cs", """
            namespace Domain;
            public class Order { public int Id { get; set; } }
            """);

        var repoInterface = ("src/Domain/IOrderRepository.cs", """
            namespace Domain;
            public interface IOrderRepository { Order Get(int id); }
            """);

        var serviceFile = ("src/Services/OrderService.cs", """
            using Domain;
            namespace Services;
            public class OrderService
            {
                private readonly IOrderRepository _repository;
                public OrderService(IOrderRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        var files = new[] { domainFile, repoInterface, serviceFile };

        // Act
        var result = _analyzer.Analyze(files, []);

        // Assert - Every emitted relationship MUST have non-null, valid Evidence
        Assert.NotEmpty(result.Relationships);
        foreach (var rel in result.Relationships)
        {
            Assert.NotNull(rel.Evidence);
            Assert.False(string.IsNullOrWhiteSpace(rel.Evidence.Location.FilePath));
            Assert.True(rel.Evidence.Location.StartLine > 0);
            Assert.True(rel.Evidence.Location.EndLine >= rel.Evidence.Location.StartLine);
            Assert.False(string.IsNullOrWhiteSpace(rel.Evidence.Snippet));

            // Confidence score must be valid
            Assert.True(rel.Evidence.Confidence.Value.Value is >= 0.0f and <= 1.0f);
        }
    }

    [Fact]
    public void Analyze_WhenEncounteringMalformedCSharpAndXml_RecoversGracefullyWithoutCrashing()
    {
        // Arrange
        var brokenCs = ("src/Broken.cs", "class { broken syntax @@@ !!! invalid tokens");
        var validCs = ("src/Valid.cs", "namespace Safe; public class ValidService {}");
        var brokenCsproj = ("src/Broken.csproj", "<Project><UnclosedTag");
        var validCsproj = ("src/Valid.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        var files = new[] { brokenCs, validCs };
        var csprojFiles = new[] { brokenCsproj, validCsproj };

        // Act
        var result = _analyzer.Analyze(files, csprojFiles);

        // Assert - Did not throw exception
        Assert.NotNull(result);

        // Valid C# file was analyzed successfully
        Assert.Contains(result.Nodes, n => n.Name == "ValidService" && n.Type == KnowledgeNodeType.Class);

        // Valid csproj was analyzed successfully
        Assert.Contains(result.PackageReferences, p => p.PackageName == "Newtonsoft.Json");

        // Malformed csproj was caught gracefully in Errors or handled without crash
        Assert.NotNull(result.Errors);
    }
}
