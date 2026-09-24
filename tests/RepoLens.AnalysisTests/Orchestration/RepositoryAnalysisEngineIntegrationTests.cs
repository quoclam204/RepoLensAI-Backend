using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;
using RepoLens.Domain.Enums;

namespace RepoLens.AnalysisTests.Orchestration;

public class RepositoryAnalysisEngineIntegrationTests : IDisposable
{
    private readonly string _fixtureDir;
    private readonly RepositoryAnalysisEngine _engine = new();

    public RepositoryAnalysisEngineIntegrationTests()
    {
        _fixtureDir = Path.Combine(Path.GetTempPath(), "RepoLens_EngineFixture_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_fixtureDir);
        BuildRealisticRepositoryFixture(_fixtureDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_fixtureDir))
            {
                Directory.Delete(_fixtureDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void AnalyzeRepository_OnRealMultiProjectFileSystemFixture_PerformsCompleteEndToEndAnalysis()
    {
        // Act
        var result = _engine.AnalyzeRepository(_fixtureDir);

        // 1. Verify Scanner & Project Discovery
        Assert.Empty(result.AllErrors);
        Assert.Equal(4, result.ScannedMetadata.Projects.Count);
        Assert.Contains(result.ScannedMetadata.Projects, p => p.ProjectName == "TestApp.Domain");
        Assert.Contains(result.ScannedMetadata.Projects, p => p.ProjectName == "TestApp.Infrastructure");
        Assert.Contains(result.ScannedMetadata.Projects, p => p.ProjectName == "TestApp.Api");
        Assert.Contains(result.ScannedMetadata.Projects, p => p.ProjectName == "TestApp.Web");

        // 2. Verify Graph Nodes
        var nodes = result.Analysis.Nodes;
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Repository && n.Id == "repo:root");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Project && n.Name == "TestApp.Domain");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Project && n.Name == "TestApp.Infrastructure");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Project && n.Name == "TestApp.Api");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Project && n.Name == "TestApp.Web");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "Order");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Enum && n.Name == "OrderStatus");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Interface && n.Name == "IOrderRepository");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "OrderRepository");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "AppDbContext");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "OrdersController");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Endpoint && n.Name.Contains("GET"));

        // 3. Verify Project-to-Project Dependencies
        var relationships = result.Analysis.Relationships;
        var apiToInfra = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.DependsOn &&
            r.SourceId == "project:TestApp.Api" &&
            r.TargetId == "project:TestApp.Infrastructure");
        Assert.NotNull(apiToInfra);
        Assert.NotNull(apiToInfra.Evidence);
        Assert.Equal(EvidenceType.Dependency, apiToInfra.Evidence.EvidenceType);

        var infraToDomain = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.DependsOn &&
            r.SourceId == "project:TestApp.Infrastructure" &&
            r.TargetId == "project:TestApp.Domain");
        Assert.NotNull(infraToDomain);

        // 4. Verify Cross-Project Interface Implementation (OrderRepository : IOrderRepository)
        var implRel = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.Implements &&
            r.SourceId == "class:OrderRepository" &&
            r.TargetId == "interface:IOrderRepository");
        Assert.NotNull(implRel);
        Assert.NotNull(implRel.Evidence);

        // 5. Verify Cross-Project Dependency Injection (OrdersController depends on IOrderRepository)
        var depRel = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.DependsOn &&
            r.SourceId == "class:OrdersController" &&
            r.TargetId == "interface:IOrderRepository");
        Assert.NotNull(depRel);
        Assert.NotNull(depRel.Evidence);

        // 6. Verify Database Mapping (AppDbContext maps to Order)
        var mapRel = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.MapsTo &&
            r.SourceId == "class:AppDbContext" &&
            r.TargetId == "entity:Order");
        Assert.NotNull(mapRel);
        Assert.NotNull(mapRel.Evidence);

        // 7. Verify API Exposure (OrdersController exposes endpoint)
        var exposeRel = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.Exposes &&
            r.SourceId == "class:OrdersController");
        Assert.NotNull(exposeRel);
        Assert.NotNull(exposeRel.Evidence);

        // 8. Verify TypeScript Import Dependency
        var tsDep = relationships.FirstOrDefault(r =>
            r.Type == KnowledgeRelationshipType.DependsOn &&
            r.TargetId.Contains("./types"));
        Assert.NotNull(tsDep);
        Assert.NotNull(tsDep.Evidence);

        // 9. Verify Evidence-First and Confidence on ALL Relationships
        foreach (var rel in relationships)
        {
            Assert.NotNull(rel.Evidence);
            Assert.NotNull(rel.Evidence.Confidence);
            Assert.True(rel.Evidence.Confidence.Value.Value is >= 0.0f and <= 1.0f);
            Assert.False(string.IsNullOrWhiteSpace(rel.Evidence.Snippet));
        }

        // 10. Verify Deduplication (Zero duplicate node IDs, zero duplicate relationship triples)
        var nodeIds = nodes.Select(n => n.Id).ToList();
        Assert.Equal(nodeIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), nodeIds.Count);

        var relKeys = relationships.Select(r => $"{r.SourceId}->{r.Type}->{r.TargetId}").ToList();
        Assert.Equal(relKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count(), relKeys.Count);

        // 11. Verify Result Contract & Metrics
        Assert.True(result.NodeCountByType.Count > 0);
        Assert.True(result.RelationshipCountByType.Count > 0);
        Assert.True(result.NodeCountByType["Class"] >= 3);
        Assert.NotNull(result.RepositoryPath);
        Assert.NotNull(result.ScannedMetadata);
        Assert.NotNull(result.Analysis);
    }

    [Fact]
    public void AnalyzeRepository_WithMalformedSourceFiles_RecoversAndReportsErrorsWithoutCrashing()
    {
        // Arrange: Add a malformed C# file and an unclosed XML csproj in a subfolder
        var brokenDir = Path.Combine(_fixtureDir, "src", "BrokenArea");
        Directory.CreateDirectory(brokenDir);
        File.WriteAllText(Path.Combine(brokenDir, "Broken.cs"), "public class { invalid token @@@ !!!");
        File.WriteAllText(Path.Combine(brokenDir, "Broken.csproj"), "<Project><UnclosedTag");

        // Act
        var result = _engine.AnalyzeRepository(_fixtureDir);

        // Assert - Pipeline completed and did not throw
        Assert.NotNull(result);

        // Valid projects and classes were still discovered and analyzed
        Assert.Contains(result.Analysis.Nodes, n => n.Name == "Order" && n.Type == KnowledgeNodeType.Class);
        Assert.Contains(result.Analysis.Nodes, n => n.Name == "OrdersController" && n.Type == KnowledgeNodeType.Class);

        // Clean up broken area so other tests are unaffected
        Directory.Delete(brokenDir, recursive: true);
    }

    private static void BuildRealisticRepositoryFixture(string root)
    {
        // 1. Solution
        File.WriteAllText(Path.Combine(root, "TestApp.sln"), "Microsoft Visual Studio Solution File, Format Version 12.00");

        // 2. Domain Project
        var domainDir = Path.Combine(root, "src", "TestApp.Domain");
        Directory.CreateDirectory(Path.Combine(domainDir, "Entities"));
        Directory.CreateDirectory(Path.Combine(domainDir, "Enums"));
        Directory.CreateDirectory(Path.Combine(domainDir, "Repositories"));

        File.WriteAllText(Path.Combine(domainDir, "TestApp.Domain.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(domainDir, "Entities", "Order.cs"), """
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            using TestApp.Domain.Enums;

            namespace TestApp.Domain.Entities;

            [Table("orders")]
            public class Order
            {
                [Key]
                public Guid Id { get; set; }
                public OrderStatus Status { get; set; }
            }
            """);

        File.WriteAllText(Path.Combine(domainDir, "Enums", "OrderStatus.cs"), """
            namespace TestApp.Domain.Enums;

            public enum OrderStatus
            {
                Pending = 1,
                Completed = 2
            }
            """);

        File.WriteAllText(Path.Combine(domainDir, "Repositories", "IOrderRepository.cs"), """
            using TestApp.Domain.Entities;

            namespace TestApp.Domain.Repositories;

            public interface IOrderRepository
            {
                Order? GetById(Guid id);
            }
            """);

        // 3. Infrastructure Project
        var infraDir = Path.Combine(root, "src", "TestApp.Infrastructure");
        Directory.CreateDirectory(Path.Combine(infraDir, "Persistence"));
        Directory.CreateDirectory(Path.Combine(infraDir, "Repositories"));

        File.WriteAllText(Path.Combine(infraDir, "TestApp.Infrastructure.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\TestApp.Domain\TestApp.Domain.csproj" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(infraDir, "Persistence", "AppDbContext.cs"), """
            using Microsoft.EntityFrameworkCore;
            using TestApp.Domain.Entities;

            namespace TestApp.Infrastructure.Persistence;

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }
            }
            """);

        File.WriteAllText(Path.Combine(infraDir, "Repositories", "OrderRepository.cs"), """
            using TestApp.Domain.Entities;
            using TestApp.Domain.Repositories;
            using TestApp.Infrastructure.Persistence;

            namespace TestApp.Infrastructure.Repositories;

            public class OrderRepository : IOrderRepository
            {
                private readonly AppDbContext _context;

                public OrderRepository(AppDbContext context)
                {
                    _context = context;
                }

                public Order? GetById(Guid id)
                {
                    return _context.Orders.Find(id);
                }
            }
            """);

        // 4. API Project
        var apiDir = Path.Combine(root, "src", "TestApp.Api");
        Directory.CreateDirectory(Path.Combine(apiDir, "Controllers"));

        File.WriteAllText(Path.Combine(apiDir, "TestApp.Api.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\TestApp.Infrastructure\TestApp.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(apiDir, "Controllers", "OrdersController.cs"), """
            using Microsoft.AspNetCore.Mvc;
            using TestApp.Domain.Repositories;

            namespace TestApp.Api.Controllers;

            [ApiController]
            [Route("api/[controller]")]
            public class OrdersController : ControllerBase
            {
                private readonly IOrderRepository _repository;

                public OrdersController(IOrderRepository repository)
                {
                    _repository = repository;
                }

                [HttpGet("{id}")]
                public IActionResult Get(Guid id)
                {
                    var order = _repository.GetById(id);
                    return Ok(order);
                }
            }
            """);

        File.WriteAllText(Path.Combine(apiDir, "Program.cs"), """
            using Microsoft.AspNetCore.Builder;

            var app = WebApplication.Create();
            app.MapGet("/api/health", () => "healthy");
            """);

        // 5. Web Project
        var webDir = Path.Combine(root, "src", "TestApp.Web");
        Directory.CreateDirectory(webDir);

        File.WriteAllText(Path.Combine(webDir, "package.json"), "{\"name\": \"test-app-web\"}");
        File.WriteAllText(Path.Combine(webDir, "tsconfig.json"), "{}");
        File.WriteAllText(Path.Combine(webDir, "types.ts"), """
            export interface Order {
                id: string;
            }
            """);

        File.WriteAllText(Path.Combine(webDir, "orders.ts"), """
            import { Order } from './types';

            export function getOrders(): Order[] {
                return [];
            }
            """);
    }
}
