using RepoLens.Analysis.CSharp;
using RepoLens.Analysis.Evidence;
using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;
using RepoLens.Analysis.Scanning;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;
using RepoLens.Infrastructure.Adapters.Analysis;

namespace RepoLens.UnitTests.Persistence;

public class AnalysisResultMapperTests
{
    [Fact]
    public void Map_ConvertsRepositoryAnalysisResult_ToAnalysisResultModel_PreservingAllData()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var projects = new List<ScannedProject>
        {
            new("OrderService", "src/OrderService/OrderService.csproj", "/repo/src/OrderService/OrderService.csproj", "CSharp")
        };
        var files = new List<ScannedFile>
        {
            new("src/OrderService/Controllers/OrderController.cs", "/repo/src/OrderService/Controllers/OrderController.cs", ".cs", 1024, "CSharp")
        };
        var scanResult = new ScannedRepository("/repo", projects, files, [], [], ["Non-fatal scan warning"]);

        var evidence = EvidenceFactory.Create(
            analysisJobId: analysisId,
            filePath: "src/OrderService/Controllers/OrderController.cs",
            startLine: 15,
            endLine: 20,
            snippet: "public IActionResult GetOrders() => Ok();",
            evidenceType: EvidenceType.Declaration,
            confidence: ConfidenceScore.High,
            symbol: "GetOrders");

        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create(
                id: "class:OrderController",
                name: "OrderController",
                type: KnowledgeNodeType.Class,
                filePath: "src/OrderService/Controllers/OrderController.cs",
                location: new SourceLocation("src/OrderService/Controllers/OrderController.cs", 10, 30)),
            KnowledgeNode.Create(
                id: "endpoint:GET:/api/orders",
                name: "GetOrders",
                type: KnowledgeNodeType.Endpoint,
                filePath: "src/OrderService/Controllers/OrderController.cs",
                location: new SourceLocation("src/OrderService/Controllers/OrderController.cs", 15, 20),
                properties: new Dictionary<string, string>
                {
                    ["HttpMethod"] = "GET",
                    ["Route"] = "/api/orders",
                    ["Controller"] = "OrderController",
                    ["Action"] = "GetOrders"
                }),
            KnowledgeNode.Create(
                id: "db:Order",
                name: "Order",
                type: KnowledgeNodeType.DatabaseEntity,
                filePath: "src/OrderService/Models/Order.cs",
                properties: new Dictionary<string, string>
                {
                    ["TableName"] = "orders"
                })
        };

        var relationships = new List<KnowledgeRelationship>
        {
            KnowledgeRelationship.Create(
                sourceId: "class:OrderController",
                targetId: "endpoint:GET:/api/orders",
                type: KnowledgeRelationshipType.Exposes,
                evidence: evidence)
        };

        var analysis = new AnalysisResult(
            Nodes: nodes.AsReadOnly(),
            Relationships: relationships.AsReadOnly(),
            ProjectReferences: [],
            PackageReferences: [],
            Errors: ["Analysis error 1"]);

        var repoResult = new RepositoryAnalysisResult(
            RepositoryPath: "/repo",
            ScannedMetadata: scanResult,
            Analysis: analysis,
            NodeCountByType: new Dictionary<string, int> { ["Class"] = 1, ["Endpoint"] = 1, ["DatabaseEntity"] = 1 },
            RelationshipCountByType: new Dictionary<string, int> { ["Exposes"] = 1 },
            AllErrors: ["Non-fatal scan warning", "Analysis error 1"]);

        // Act
        var resultModel = AnalysisResultMapper.Map(repoResult, analysisId);

        // Assert
        Assert.Equal(analysisId, resultModel.AnalysisId);
        Assert.Single(resultModel.Projects);
        Assert.Equal("OrderService", resultModel.Projects[0].Name);

        Assert.Single(resultModel.SourceFiles);
        Assert.Equal("src/OrderService/Controllers/OrderController.cs", resultModel.SourceFiles[0].Path);

        Assert.Single(resultModel.CodeSymbols);
        Assert.Equal("OrderController", resultModel.CodeSymbols[0].Name);
        Assert.Equal(SymbolType.Class, resultModel.CodeSymbols[0].SymbolType);

        Assert.Single(resultModel.ApiEndpoints);
        Assert.Equal("GET", resultModel.ApiEndpoints[0].Method);
        Assert.Equal("/api/orders", resultModel.ApiEndpoints[0].Route);

        Assert.Single(resultModel.DatabaseEntities);
        Assert.Equal("Order", resultModel.DatabaseEntities[0].Name);

        Assert.Single(resultModel.Dependencies);
        Assert.Equal("class:OrderController", resultModel.Dependencies[0].SourceId);
        Assert.Equal("endpoint:GET:/api/orders", resultModel.Dependencies[0].TargetId);
        Assert.Equal(DependencyType.Exposes, resultModel.Dependencies[0].DependencyType);
        Assert.NotNull(resultModel.Dependencies[0].EvidenceKey);

        Assert.Single(resultModel.Evidences);
        Assert.Equal("src/OrderService/Controllers/OrderController.cs", resultModel.Evidences[0].FilePath);
        Assert.Equal(15, resultModel.Evidences[0].StartLine);
        Assert.Equal(20, resultModel.Evidences[0].EndLine);

        Assert.Equal(2, resultModel.Issues.Count);
    }
}
