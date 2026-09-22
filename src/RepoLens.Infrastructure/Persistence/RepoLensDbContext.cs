using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for RepoLens AI backend persistence (T010).
/// Isolated from Domain entities via Fluent API configurations.
/// </summary>
public class RepoLensDbContext : DbContext
{
    public RepoLensDbContext(DbContextOptions<RepoLensDbContext> options)
        : base(options)
    {
    }

    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SourceFile> SourceFiles => Set<SourceFile>();
    public DbSet<CodeSymbol> CodeSymbols => Set<CodeSymbol>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<ApiEndpoint> ApiEndpoints => Set<ApiEndpoint>();
    public DbSet<DatabaseEntity> DatabaseEntities => Set<DatabaseEntity>();
    public DbSet<DatabaseRelationship> DatabaseRelationships => Set<DatabaseRelationship>();
    public DbSet<Evidence> Evidences => Set<Evidence>();
    public DbSet<AnalysisIssue> AnalysisIssues => Set<AnalysisIssue>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity type configurations defined in the Infrastructure assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RepoLensDbContext).Assembly);
    }
}
