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

    [Fact]
    public void ConvertFromArchitectureResponse_PreservesContainersAndEvidence()
    {
        // Arrange
        var response = new RepoLens.Application.DTOs.Architecture.ArchitectureResponse(
            AnalysisId: Guid.NewGuid(),
            Nodes:
            [
                new("proj:repolens-api", "Project", "RepoLens.Api", "src/RepoLens.Api"),
                new("cls:analysiscontroller", "Class", "AnalysisController", "src/RepoLens.Api/Controllers/AnalysisController.cs")
            ],
            Edges:
            [
                new("edge-1", "repolens-api", "repolens-domain", "References", "confirmed",
                    new RepoLens.Application.DTOs.Architecture.EvidenceSnippetDto("src/RepoLens.Api/RepoLens.Api.csproj", 10, 15),
                    "ev:10-15")
            ]);

        var adapter = new ArchifyAdapter();

        // Act
        var doc = adapter.ConvertFromArchitectureResponse(response);

        // Assert
        Assert.NotNull(doc);
        Assert.Single(doc.System.Containers);
        Assert.Equal("RepoLens.Api", doc.System.Containers[0].Name);
        Assert.Single(doc.System.Containers[0].Components);
        Assert.Equal("AnalysisController", doc.System.Containers[0].Components[0].Name);

        Assert.Single(doc.System.Relationships);
        Assert.Equal("References", doc.System.Relationships[0].Type);
        Assert.Contains("ev:10-15", doc.System.Relationships[0].EvidenceIds);
    }

    [Fact]
    public void ConvertToArchifyV3_GeneratesValidV3SpecAndStandaloneHtml()
    {
        // Arrange
        var response = new RepoLens.Application.DTOs.Architecture.ArchitectureResponse(
            Guid.NewGuid(),
            [
                new("node-1", "Project", "RepoLens.Api", "src/RepoLens.Api"),
                new("node-2", "Project", "RepoLens.Application", "src/RepoLens.Application")
            ],
            [
                new("edge-1", "RepoLens.Api", "RepoLens.Application", "ProjectReference", "confirmed")
            ]);

        var adapter = new ArchifyAdapter();

        // Act
        var v3Doc = adapter.ConvertToArchifyV3(response, "Test System");
        var html = adapter.GenerateStandaloneHtml(v3Doc, "dark");

        // Assert
        Assert.NotNull(v3Doc);
        Assert.Equal("architecture", v3Doc.DiagramType);
        Assert.Equal("Test System", v3Doc.Meta.Title);
        Assert.Equal(2, v3Doc.Components.Count);
        Assert.Single(v3Doc.Connections);

        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Test System", html);
        Assert.Contains("svg", html);
    }
}

