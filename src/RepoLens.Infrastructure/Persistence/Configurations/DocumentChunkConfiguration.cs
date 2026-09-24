using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(c => c.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(c => c.AnalysisId);
        builder.HasIndex(c => c.SourceFileId);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.TokenCount)
            .IsRequired();

        builder.Property(c => c.ChunkIndex)
            .IsRequired();

        // In-memory / transient RAG metadata properties (T083)
        builder.Ignore(c => c.StartLine);
        builder.Ignore(c => c.EndLine);
        builder.Ignore(c => c.ConfidenceScore);
        builder.Ignore(c => c.EvidenceIds);

        builder.HasOne(c => c.Analysis)
            .WithMany(a => a.DocumentChunks)
            .HasForeignKey(c => c.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.SourceFile)
            .WithMany(f => f.DocumentChunks)
            .HasForeignKey(c => c.SourceFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Evidence)
            .WithMany()
            .HasForeignKey(c => c.EvidenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
