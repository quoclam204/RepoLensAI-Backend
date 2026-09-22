using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class DatabaseEntityExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFullFromTree_WithFluentApiAndRelationships_ExtractsContextEntitiesAndRelationships()
    {
        // Arrange
        var code = """
            using Microsoft.EntityFrameworkCore;

            namespace Demo;

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; }

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasOne(x => x.Customer)
                        .WithMany(x => x.Orders)
                        .HasForeignKey(x => x.CustomerId);
                }
            }

            public class Order
            {
                public int Id { get; set; }
                public int CustomerId { get; set; }
            }

            public class Customer
            {
                public int Id { get; set; }
            }
            """;

        var tree = _parser.ParseText(code, "AppDbContext.cs");

        // Act
        var result = DatabaseEntityExtractor.ExtractAllFromTree(tree, "AppDbContext.cs");

        // Assert - Context
        Assert.Single(result.Contexts);
        var context = result.Contexts[0];
        Assert.Equal("AppDbContext", context.ContextName);
        Assert.Equal("AppDbContext.cs", context.Location.FilePath);
        Assert.True(context.Location.StartLine > 0);

        // Assert - DbSet
        Assert.Single(result.DbSets);
        var dbSet = result.DbSets[0];
        Assert.Equal("Order", dbSet.EntityTypeName);
        Assert.Equal("Orders", dbSet.PropertyName);
        Assert.Equal("AppDbContext", dbSet.ContainingContextName);

        // Assert - Entities (from DbSet<Order>)
        Assert.Contains(result.Entities, e => e.EntityName == "Order");

        // Assert - Fluent API relationship
        Assert.Single(result.Relationships);
        var rel = result.Relationships[0];
        Assert.Equal("Order", rel.PrincipalEntity);
        Assert.Equal("Customer", rel.DependentEntity);
        Assert.Equal("OneToMany", rel.Multiplicity); // HasOne(...).WithMany(...)
        Assert.Equal("CustomerId", rel.ForeignKey);
        Assert.Equal("AppDbContext.cs", rel.Location.FilePath);
        Assert.True(rel.Location.StartLine > 0);
        Assert.Contains("HasForeignKey", rel.Snippet);
    }

    [Fact]
    public void ExtractFromTree_WithDbContextAndEntities_ExtractsExpectedDatabaseStructures()
    {
        // Arrange
        var code = """
            using Microsoft.EntityFrameworkCore;
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;

            public class AppDbContext : DbContext
            {
                public DbSet<User> Users { get; set; }
            }

            [Table("tbl_user")]
            public class User
            {
                [Key]
                public Guid Id { get; set; }
            }
            """;

        var tree = _parser.ParseText(code, "Data.cs");

        // Act
        var (contexts, dbSets, entities) = DatabaseEntityExtractor.ExtractFromTree(tree, "Data.cs");

        // Assert
        Assert.Single(contexts);
        Assert.Equal("AppDbContext", contexts[0].ContextName);

        Assert.Single(dbSets);
        Assert.Equal("User", dbSets[0].EntityTypeName);
        Assert.Equal("Users", dbSets[0].PropertyName);

        Assert.Single(entities);
        Assert.Equal("User", entities[0].EntityName);
        Assert.Equal("tbl_user", entities[0].TableName);
        Assert.Contains("Id", entities[0].KeyProperties);
    }

    [Fact]
    public void ExtractFromTree_WhenClassIsNotDbContextAndHasNoDatabaseAttributes_ReturnsEmptyResults()
    {
        // Arrange (Negative case)
        var code = """
            namespace MyNamespace;

            public class NotificationDto
            {
                public string Title { get; set; } = string.Empty;
                public string Message { get; set; } = string.Empty;
            }

            public class SimpleHelper
            {
                public int Add(int x, int y) => x + y;
            }
            """;

        var tree = _parser.ParseText(code, "Helper.cs");

        // Act
        var (contexts, dbSets, entities) = DatabaseEntityExtractor.ExtractFromTree(tree, "Helper.cs");

        // Assert
        Assert.Empty(contexts);
        Assert.Empty(dbSets);
        Assert.Empty(entities);
    }
}
