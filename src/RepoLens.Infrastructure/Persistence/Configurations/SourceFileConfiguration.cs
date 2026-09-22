using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class SourceFileConfiguration : IEntityTypeConfiguration<SourceFile>
{
    public void Configure(EntityTypeBuilder<SourceFile> builder)
    {
        builder.ToTable("source_files");

        builder.HasKey(f => f.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(f => f.AnalysisId);
        builder.HasIndex(f => f.ProjectId);
        builder.HasIndex(f => new { f.AnalysisId, f.Path });

        builder.Property(f => f.Path)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(f => f.Language)
            .HasMaxLength(50);

        builder.Property(f => f.Hash)
            .HasMaxLength(128);

        builder.Property(f => f.AnalysisStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(f => f.Analysis)
            .WithMany(a => a.SourceFiles)
            .HasForeignKey(f => f.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Project)
            .WithMany(p => p.SourceFiles)
            .HasForeignKey(f => f.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Symbols)
            .WithOne(s => s.SourceFile)
            .HasForeignKey(s => s.SourceFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.DocumentChunks)
            .WithOne(c => c.SourceFile)
            .HasForeignKey(c => c.SourceFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
