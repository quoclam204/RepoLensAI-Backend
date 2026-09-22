using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class AnalysisConfiguration : IEntityTypeConfiguration<Analysis>
{
    public void Configure(EntityTypeBuilder<Analysis> builder)
    {
        builder.ToTable("analyses");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.RepositoryId);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.CurrentStage)
            .HasMaxLength(100);

        builder.Property(a => a.CommitHash)
            .HasMaxLength(64);

        builder.Property(a => a.StartedAt)
            .IsRequired();

        builder.Property(a => a.Error)
            .HasColumnType("text");

        builder.HasMany(a => a.Projects)
            .WithOne(p => p.Analysis)
            .HasForeignKey(p => p.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.SourceFiles)
            .WithOne(f => f.Analysis)
            .HasForeignKey(f => f.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Dependencies)
            .WithOne(d => d.Analysis)
            .HasForeignKey(d => d.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.ApiEndpoints)
            .WithOne(e => e.Analysis)
            .HasForeignKey(e => e.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.DatabaseEntities)
            .WithOne(d => d.Analysis)
            .HasForeignKey(d => d.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.DatabaseRelationships)
            .WithOne(r => r.Analysis)
            .HasForeignKey(r => r.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Evidences)
            .WithOne(e => e.Analysis)
            .HasForeignKey(e => e.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Issues)
            .WithOne(i => i.Analysis)
            .HasForeignKey(i => i.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.DocumentChunks)
            .WithOne(c => c.Analysis)
            .HasForeignKey(c => c.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
