using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ToTable("evidences");

        builder.HasKey(e => e.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(e => e.AnalysisId);
        builder.HasIndex(e => e.FilePath);

        builder.Property(e => e.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.Symbol)
            .HasMaxLength(500);

        builder.Property(e => e.EvidenceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Description)
            .HasColumnType("text");

        builder.HasOne(e => e.Analysis)
            .WithMany(a => a.Evidences)
            .HasForeignKey(e => e.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
