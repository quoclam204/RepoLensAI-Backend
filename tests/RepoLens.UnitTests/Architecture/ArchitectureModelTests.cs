using RepoLens.Application.Models.Architecture;

namespace RepoLens.UnitTests.Architecture;

public class ArchitectureModelTests
{
    [Fact]
    public void ArchitectureModel_RepresentsContainersAndComponents_WithEvidence()
    {
        // Arrange
        var nodes = new List<ArchitectureNode>
        {
            new("container:api", "OrderApi", ArchitectureNodeType.Container, "src/OrderApi", ".NET 10", null, new Dictionary<string, string>()),
            new("comp:controller", "OrderController", ArchitectureNodeType.Component, "src/OrderApi/Controllers/OrderController.cs", "C#", "container:api", new Dictionary<string, string>())
        };

        var relationships = new List<ArchitectureRelationship>
        {
            new("comp:controller", "comp:service", "Calls", "High", ["ev:101"])
        };

        var evidences = new List<ArchitectureEvidence>
        {
            new("ev:101", "src/OrderApi/Controllers/OrderController.cs", 25, 30, "_service.Process();", "High")
        };

        // Act
        var model = new ArchitectureModel("OrderSystem", "E-commerce order system", nodes, relationships, evidences);

        // Assert
        Assert.Equal("OrderSystem", model.SystemName);
        Assert.Equal(2, model.Nodes.Count);
        Assert.Single(model.Relationships);
        Assert.Single(model.Evidences);
        Assert.Equal("Calls", model.Relationships[0].RelationshipType);
        Assert.Equal("ev:101", model.Relationships[0].EvidenceIds[0]);
    }
}
