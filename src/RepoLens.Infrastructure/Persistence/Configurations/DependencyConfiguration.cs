using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class DependencyConfiguration : IEntityTypeConfiguration<Dependency>
{
    public void Configure(EntityTypeBuilder<Dependency> builder)
    {
        builder.ToTable("dependencies");

        builder.HasKey(d => d.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(d => d.AnalysisId);
        builder.HasIndex(d => d.SourceId);
        builder.HasIndex(d => d.TargetId);

        builder.Property(d => d.SourceId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.TargetId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.DependencyType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(d => d.Analysis)
            .WithMany(a => a.Dependencies)
            .HasForeignKey(d => d.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Evidence)
            .WithMany()
            .HasForeignKey(d => d.EvidenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
