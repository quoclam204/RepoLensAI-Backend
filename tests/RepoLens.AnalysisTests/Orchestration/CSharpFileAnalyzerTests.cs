using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;

namespace RepoLens.AnalysisTests.Orchestration;

public class CSharpFileAnalyzerTests
{
    private readonly CSharpFileAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_WithComprehensiveSource_ExtractsNodesAndKeyRelationships()
    {
        // Arrange
        var code = """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.EntityFrameworkCore;
            using System.ComponentModel.DataAnnotations;

            namespace SampleApp;

            public interface IOrderRepository {}

            public interface IOrderService
            {
                void ProcessOrder();
            }

            public class OrderService : IOrderService
            {
                private readonly IOrderRepository _repository;

                public OrderService(IOrderRepository repository)
                {
                    _repository = repository;
                }

                public void ProcessOrder()
                {
                    LogProcessing();
                }

                private void LogProcessing() {}
            }

            [ApiController]
            [Route("api/[controller]")]
            public class OrdersController : ControllerBase
            {
                private readonly IOrderService _service;

                public OrdersController(IOrderService service)
                {
                    _service = service;
                }

                [HttpGet("active")]
                public IActionResult GetActive() => Ok();
            }

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }

            public class Order
            {
                [Key]
                public Guid Id { get; set; }
            }
            """;

        // Act
        var result = _analyzer.Analyze("Sample.cs", code);

        // Assert Nodes
        Assert.Empty(result.Errors);
        Assert.Contains(result.Nodes, n => n.Type == KnowledgeNodeType.Interface && n.Name == "IOrderService");
        Assert.Contains(result.Nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "OrderService");
        Assert.Contains(result.Nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "OrdersController");
        Assert.Contains(result.Nodes, n => n.Type == KnowledgeNodeType.Endpoint && n.Name.Contains("GET"));
        Assert.Contains(result.Nodes, n => n.Type == KnowledgeNodeType.DatabaseEntity && n.Name == "Order");

        // Assert Relationships
        Assert.Contains(result.Relationships, r => r.Type == KnowledgeRelationshipType.Implements);
        Assert.Contains(result.Relationships, r => r.Type == KnowledgeRelationshipType.DependsOn);
        Assert.Contains(result.Relationships, r => r.Type == KnowledgeRelationshipType.Calls);
        Assert.Contains(result.Relationships, r => r.Type == KnowledgeRelationshipType.Exposes);
        Assert.Contains(result.Relationships, r => r.Type == KnowledgeRelationshipType.MapsTo);
    }

    [Fact]
    public void Analyze_WhenSourceHasSyntaxCrashOrEmpty_ReturnsSafeResultsWithoutThrowing()
    {
        // Arrange (Negative case)
        var emptyCode = "   \n\t  ";

        // Act
        var result = _analyzer.Analyze("Empty.cs", emptyCode);

        // Assert
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Relationships);
        Assert.Empty(result.Errors);
    }
}
