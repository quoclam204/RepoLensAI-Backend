using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.Orchestration;

public class ProjectAnalyzerTests
{
    private readonly ProjectAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_WithCrossFileInheritanceAndCsproj_ResolvesCrossFileRelationshipsAndDependencies()
    {
        // Arrange
        // File 1 defines BaseService in ServiceBase.cs
        var file1 = (
            FilePath: "src/Services/ServiceBase.cs",
            SourceText: """
                namespace MySystem.Services;

                public abstract class BaseService
                {
                    public void CommonLog() {}
                }
                """
        );

        // File 2 defines OrderService extending BaseService in OrderService.cs
        var file2 = (
            FilePath: "src/Services/OrderService.cs",
            SourceText: """
                namespace MySystem.Services;

                public class OrderService : BaseService
                {
                    public void Run() {}
                }
                """
        );

        var csproj = (
            CsprojPath: "src/Services/Services.csproj",
            CsprojContent: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="..\Core\Core.csproj" />
                  </ItemGroup>
                  <ItemGroup>
                    <PackageReference Include="MediatR" Version="12.4.1" />
                  </ItemGroup>
                </Project>
                """
        );

        // Act
        var result = _analyzer.Analyze([file1, file2], [csproj]);

        // Assert Nodes
        Assert.Empty(result.Errors);
        Assert.Contains(result.Nodes, n => n.Name == "BaseService" && n.Type == KnowledgeNodeType.Class);
        Assert.Contains(result.Nodes, n => n.Name == "OrderService" && n.Type == KnowledgeNodeType.Class);

        // Assert Cross-File Relationship: OrderService INHERITS BaseService
        var inheritRel = result.Relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.Inherits &&
            r.SourceId == "class:OrderService" &&
            r.TargetId == "class:BaseService");

        Assert.NotNull(inheritRel);

        // Assert Project and Package Dependencies
        Assert.Single(result.ProjectReferences);
        Assert.Equal("Core", result.ProjectReferences[0].TargetProjectName);
        Assert.Equal(ConfidenceScore.High, result.ProjectReferences[0].Confidence);

        Assert.Single(result.PackageReferences);
        Assert.Equal("MediatR", result.PackageReferences[0].PackageName);
        Assert.Equal("12.4.1", result.PackageReferences[0].Version);
        Assert.Equal(ConfidenceScore.High, result.PackageReferences[0].Confidence);
    }

    [Fact]
    public void Analyze_WhenFileListIsEmpty_ReturnsEmptyResultGracefully()
    {
        // Arrange (Negative case)
        var emptyFiles = Array.Empty<(string FilePath, string SourceText)>();
        var emptyCsprojs = Array.Empty<(string CsprojPath, string CsprojContent)>();

        // Act
        var result = _analyzer.Analyze(emptyFiles, emptyCsprojs);

        // Assert
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Relationships);
        Assert.Empty(result.ProjectReferences);
        Assert.Empty(result.PackageReferences);
        Assert.Empty(result.Errors);
    }
}
