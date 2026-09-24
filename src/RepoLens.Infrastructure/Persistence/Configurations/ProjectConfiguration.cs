using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(p => p.AnalysisId);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Language)
            .HasMaxLength(50);

        builder.Property(p => p.ProjectType)
            .HasMaxLength(100);

        builder.HasOne(p => p.Analysis)
            .WithMany(a => a.Projects)
            .HasForeignKey(p => p.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.SourceFiles)
            .WithOne(f => f.Project)
            .HasForeignKey(f => f.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ApiEndpoints)
            .WithOne(e => e.Project)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
