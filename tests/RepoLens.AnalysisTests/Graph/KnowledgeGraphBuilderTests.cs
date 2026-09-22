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
