using RepoLens.Application.DTOs.Persistence;
using RepoLens.Application.Models.Architecture;
using RepoLens.Application.Services;
using RepoLens.Domain.Enums;

namespace RepoLens.UnitTests.Archify;

public class ArchifyAdapterTests
{
    [Fact]
    public void ConvertToArchify_FromArchitectureModel_ConformsToArchifySchema()
    {
        // Arrange
        var nodes = new List<ArchitectureNode>
        {
            new("container:repolens-api", "RepoLens.Api", ArchitectureNodeType.Container, "src/RepoLens.Api", ".NET 10", null, new Dictionary<string, string>()),
            new("comp:analysis-controller", "AnalysisController", ArchitectureNodeType.Component, "src/RepoLens.Api/Controllers/AnalysisController.cs", "C#", "container:repolens-api", new Dictionary<string, string>())
        };

        var relationships = new List<ArchitectureRelationship>
        {
            new("container:repolens-api", "container:repolens-application", "References", "High", ["ev_201"])
        };

        var evidences = new List<ArchitectureEvidence>
        {
            new("ev_101", "src/RepoLens.Api/Controllers/AnalysisController.cs", 10, 20, "public class AnalysisController", "High")
        };

        var archModel = new ArchitectureModel("RepoLensAI", "Intelligence platform", nodes, relationships, evidences);
        var adapter = new ArchifyAdapter();

        // Act
        var doc = adapter.ConvertToArchify(archModel);

        // Assert
        Assert.NotNull(doc.System);
        Assert.Equal("RepoLensAI", doc.System.Name);
        Assert.Single(doc.System.Containers);

        var container = doc.System.Containers[0];
        Assert.Equal("RepoLens.Api", container.Name);
        Assert.Single(container.Components);
        Assert.Equal("AnalysisController", container.Components[0].Name);

        Assert.Single(doc.System.Relationships);
        Assert.Equal("References", doc.System.Relationships[0].Type);
        Assert.Equal("ev_201", doc.System.Relationships[0].EvidenceIds[0]);
    }

    [Fact]
    public void ConvertFromAnalysis_FromAnalysisResultModel_PreservesProjectsAndDependencies()
    {
        // Arrange
        var analysisModel = new AnalysisResultModel
        {
            AnalysisId = Guid.NewGuid(),
            Projects =
            [
                new(Guid.NewGuid(), "RepoLens.Api", "src/RepoLens.Api/RepoLens.Api.csproj", "csharp", "CSharp"),
                new(Guid.NewGuid(), "RepoLens.Domain", "src/RepoLens.Domain/RepoLens.Domain.csproj", "csharp", "CSharp")
            ],
            CodeSymbols =
            [
                new(Guid.NewGuid(), "class:AnalysesController", "src/RepoLens.Api/Controllers/AnalysesController.cs", null, "AnalysesController", "RepoLens.Api.Controllers.AnalysesController", SymbolType.Class, 10, 50)
            ],
            Dependencies =
            [
                new(Guid.NewGuid(), "project:RepoLens.Api", "project:RepoLens.Domain", DependencyType.ProjectReference, "ev:csproj:10-12", null)
            ],
            Evidences =
            [
                new(Guid.NewGuid(), "ev:csproj:10-12", "src/RepoLens.Api/RepoLens.Api.csproj", null, 10, 12, EvidenceType.ProjectDependency, "<ProjectReference Include=\"..\\RepoLens.Domain\" />")
            ]
        };

        var adapter = new ArchifyAdapter();

        // Act
        var doc = adapter.ConvertFromAnalysis(analysisModel);

        // Assert
        Assert.Equal(2, doc.System.Containers.Count);
        var apiContainer = doc.System.Containers.First(c => c.Name == "RepoLens.Api");
        Assert.Equal("WebApi", apiContainer.Type);
        Assert.Single(apiContainer.Components);
        Assert.Equal("AnalysesController", apiContainer.Components[0].Name);

        Assert.Single(doc.System.Relationships);
        var rel = doc.System.Relationships[0];
        Assert.Equal("ProjectReference", rel.Type);
        Assert.Equal("confirmed", rel.Confidence);
        Assert.Contains("ev:csproj:10-12", rel.EvidenceIds);
    }
}
