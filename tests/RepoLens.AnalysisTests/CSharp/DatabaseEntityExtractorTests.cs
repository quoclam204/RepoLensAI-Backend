using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class DatabaseEntityExtractorTests
{
    private readonly CSharpFileParser _parser = new();

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
