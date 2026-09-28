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

    [Fact]
    public void Map_WhenChunksHaveNoPriorEvidence_SynthesizesEvidenceAndMaintainsProvenance()
    {
        // Arrange: A repo with only a documentation file and no relationship evidences
        var analysisId = Guid.NewGuid();
        var files = new List<ScannedFile>
        {
            new("docs/readme.md", "/repo/docs/readme.md", ".md", 50, "Documentation", "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")
        };
        var scanResult = new ScannedRepository("/repo", [], files, [], [], []);

        var analysis = new AnalysisResult([], [], [], [], []);
        var repoResult = new RepositoryAnalysisResult("/repo", scanResult, analysis, new Dictionary<string, int>(), new Dictionary<string, int>(), []);

        var fileContents = new Dictionary<string, string>
        {
            ["docs/readme.md"] = "# Readme Title\nSome content"
        };

        // Act
        var resultModel = AnalysisResultMapper.Map(repoResult, analysisId, fileContents);

        // Assert
        Assert.Single(resultModel.SourceFiles);
        Assert.Equal("abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", resultModel.SourceFiles[0].Hash);

        Assert.NotEmpty(resultModel.DocumentChunks);
        foreach (var chunk in resultModel.DocumentChunks)
        {
            Assert.NotNull(chunk.EvidenceId);
            Assert.Contains(resultModel.Evidences, e => e.Id == chunk.EvidenceId.Value);
            Assert.Equal(resultModel.SourceFiles[0].Id, chunk.SourceFileId);
        }
    }

    [Fact]
    public void Map_WithSameInput_ProducesIdenticalDeterministicIdsAcrossRuns()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var projects = new List<ScannedProject>
        {
            new("OrderService", "src/OrderService/OrderService.csproj", "/repo/src/OrderService/OrderService.csproj", "CSharp")
        };
        var files = new List<ScannedFile>
        {
            new("src/OrderService/OrderController.cs", "/repo/src/OrderService/OrderController.cs", ".cs", 100, "CSharp", "hash123")
        };
        var scanResult = new ScannedRepository("/repo", projects, files, [], [], ["Scan error"]);

        var evidence = EvidenceFactory.Create(
            analysisJobId: analysisId,
            filePath: "src/OrderService/OrderController.cs",
            startLine: 1,
            endLine: 10,
            snippet: "class OrderController {}",
            evidenceType: EvidenceType.Declaration,
            confidence: ConfidenceScore.High,
            symbol: "OrderController");

        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create("class:OrderController", "OrderController", KnowledgeNodeType.Class, "src/OrderService/OrderController.cs", new SourceLocation("src/OrderService/OrderController.cs", 1, 10)),
            KnowledgeNode.Create("endpoint:GET:/api/orders", "GET /api/orders", KnowledgeNodeType.Endpoint, "src/OrderService/OrderController.cs", new SourceLocation("src/OrderService/OrderController.cs", 2, 5), new Dictionary<string, string> { ["HttpMethod"] = "GET", ["Route"] = "/api/orders" }),
            KnowledgeNode.Create("dbentity:Order", "Order", KnowledgeNodeType.DatabaseEntity, "src/OrderService/Order.cs")
        };

        var relationships = new List<KnowledgeRelationship>
        {
            KnowledgeRelationship.Create("class:OrderController", "endpoint:GET:/api/orders", KnowledgeRelationshipType.Exposes, evidence)
        };

        var analysis = new AnalysisResult(nodes, relationships, [], [], ["Analysis warning"]);
        var repoResult = new RepositoryAnalysisResult("/repo", scanResult, analysis, new Dictionary<string, int>(), new Dictionary<string, int>(), ["Scan error", "Analysis warning"]);

        var fileContents = new Dictionary<string, string>
        {
            ["src/OrderService/OrderController.cs"] = "public class OrderController {}"
        };

        // Act
        var run1 = AnalysisResultMapper.Map(repoResult, analysisId, fileContents);
        var run2 = AnalysisResultMapper.Map(repoResult, analysisId, fileContents);

        // Assert: All generated GUIDs must be exactly identical
        Assert.Equal(run1.Projects[0].Id, run2.Projects[0].Id);
        Assert.Equal(run1.SourceFiles[0].Id, run2.SourceFiles[0].Id);
        Assert.Equal(run1.CodeSymbols[0].Id, run2.CodeSymbols[0].Id);
        Assert.Equal(run1.ApiEndpoints[0].Id, run2.ApiEndpoints[0].Id);
        Assert.Equal(run1.DatabaseEntities[0].Id, run2.DatabaseEntities[0].Id);
        Assert.Equal(run1.Dependencies[0].Id, run2.Dependencies[0].Id);
        Assert.Equal(run1.Evidences[0].Id, run2.Evidences[0].Id);
        Assert.Equal(run1.Issues[0].Id, run2.Issues[0].Id);
        Assert.Equal(run1.DocumentChunks[0].Id, run2.DocumentChunks[0].Id);
    }

    [Fact]
    public void Map_WithDuplicateRelationshipsAndEndpoints_DeduplicatesAppropriately()
    {
        // Arrange
        var analysisId = Guid.NewGuid();
        var projects = new List<ScannedProject>
        {
            new("App", "src/App.csproj", "/repo/src/App.csproj", "CSharp")
        };
        var files = new List<ScannedFile>
        {
            new("src/File.cs", "/repo/src/File.cs", ".cs", 100, "CSharp")
        };
        var scanResult = new ScannedRepository("/repo", projects, files, [], [], []);

        // Duplicate endpoints in nodes
        var nodes = new List<KnowledgeNode>
        {
            KnowledgeNode.Create("endpoint:GET:/api/items", "GET /api/items", KnowledgeNodeType.Endpoint, "src/File.cs", properties: new Dictionary<string, string> { ["HttpMethod"] = "GET", ["Route"] = "/api/items" }),
            KnowledgeNode.Create("endpoint:GET:/api/items", "GET /api/items", KnowledgeNodeType.Endpoint, "src/File.cs", properties: new Dictionary<string, string> { ["HttpMethod"] = "GET", ["Route"] = "/api/items" })
        };

        // Duplicate relationships
        var relationships = new List<KnowledgeRelationship>
        {
            KnowledgeRelationship.Create("nodeA", "nodeB", KnowledgeRelationshipType.DependsOn),
            KnowledgeRelationship.Create("nodeA", "nodeB", KnowledgeRelationshipType.DependsOn)
        };

        var analysis = new AnalysisResult(nodes, relationships, [], [], []);
        var repoResult = new RepositoryAnalysisResult("/repo", scanResult, analysis, new Dictionary<string, int>(), new Dictionary<string, int>(), []);

        // Act
        var result = AnalysisResultMapper.Map(repoResult, analysisId);

        // Assert - Duplicates are eliminated
        Assert.Single(result.ApiEndpoints);
        Assert.Single(result.Dependencies);
    }
}
