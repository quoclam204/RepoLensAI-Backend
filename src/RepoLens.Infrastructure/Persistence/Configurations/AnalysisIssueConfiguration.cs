using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class AnalysisIssueConfiguration : IEntityTypeConfiguration<AnalysisIssue>
{
    public void Configure(EntityTypeBuilder<AnalysisIssue> builder)
    {
        builder.ToTable("analysis_issues");

        builder.HasKey(i => i.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(i => i.AnalysisId);

        builder.Property(i => i.FilePath)
            .HasMaxLength(1000);

        builder.Property(i => i.IssueType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.Message)
            .IsRequired()
            .HasColumnType("text");

        builder.HasOne(i => i.Analysis)
            .WithMany(a => a.Issues)
            .HasForeignKey(i => i.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
