using RepoLens.Analysis.Graph;

namespace RepoLens.AnalysisTests.Graph;

public class KnowledgeGraphBuilderTests
{
    [Fact]
    public void KnowledgeGraphBuilder_BuildsNodesAndRelationships()
    {
        // Arrange
        var builder = new KnowledgeGraphBuilder();
        var node1 = KnowledgeNode.Create("class:UserRepo", "UserRepo", KnowledgeNodeType.Class, "UserRepo.cs");
        var node2 = KnowledgeNode.Create("interface:IRepo", "IRepo", KnowledgeNodeType.Interface, "IRepo.cs");

        // Act & Assert
        Assert.True(builder.AddNode(node1));
        Assert.True(builder.AddNode(node2));
        Assert.False(builder.AddNode(node1)); // Duplicate ID rejection

        var rel = KnowledgeRelationship.Create(node1.Id, node2.Id, KnowledgeRelationshipType.Implements);
        builder.AddRelationship(rel);

        Assert.Equal(2, builder.Nodes.Count);
        Assert.Single(builder.Relationships);
        Assert.Single(builder.GetOutgoingRelationships("class:UserRepo"));
        Assert.Single(builder.GetIncomingRelationships("interface:IRepo"));
    }

    [Fact]
    public void AddRelationship_WithDuplicateEdges_DeduplicatesEdgesSilently()
    {
        // Arrange
        var builder = new KnowledgeGraphBuilder();
        var node1 = KnowledgeNode.Create("class:OrderService", "OrderService", KnowledgeNodeType.Class, "OrderService.cs");
        var node2 = KnowledgeNode.Create("interface:IOrderService", "IOrderService", KnowledgeNodeType.Interface, "IOrderService.cs");

        builder.AddNode(node1);
        builder.AddNode(node2);

        var rel1 = KnowledgeRelationship.Create("class:OrderService", "interface:IOrderService", KnowledgeRelationshipType.Implements);
        var rel2 = KnowledgeRelationship.Create("class:OrderService", "interface:IOrderService", KnowledgeRelationshipType.Implements);

        // Act
        builder.AddRelationship(rel1);
        builder.AddRelationship(rel2); // Duplicate edge attempt

        // Assert
        Assert.Single(builder.Relationships);
        Assert.Single(builder.GetOutgoingRelationships("class:OrderService"));
    }

    [Fact]
    public void AddOrUpdateNode_WithExistingId_UpdatesPropertiesWithoutDuplicatingNode()
    {
        // Arrange
        var builder = new KnowledgeGraphBuilder();
        var nodeOriginal = KnowledgeNode.Create("class:Order", "Order", KnowledgeNodeType.Class, "Order.cs");
        nodeOriginal.Properties["Version"] = "1.0";

        var nodeUpdated = KnowledgeNode.Create("class:Order", "Order", KnowledgeNodeType.Class, "Order.cs");
        nodeUpdated.Properties["Version"] = "2.0";

        // Act
        builder.AddNode(nodeOriginal);
        builder.AddOrUpdateNode(nodeUpdated);

        // Assert
        Assert.Single(builder.Nodes);
        Assert.True(builder.TryGetNode("class:Order", out var stored));
        Assert.NotNull(stored);
        Assert.Equal("2.0", stored.Properties["Version"]);
    }

    [Fact]
    public void GetRelationships_WhenNodeIdDoesNotExist_ReturnsEmptyList()
    {
        // Arrange (Negative case)
        var builder = new KnowledgeGraphBuilder();

        // Act
        var outgoing = builder.GetOutgoingRelationships("non:existent");
        var incoming = builder.GetIncomingRelationships("non:existent");

        // Assert
        Assert.Empty(outgoing);
        Assert.Empty(incoming);
        Assert.False(builder.TryGetNode("non:existent", out _));
    }
}
