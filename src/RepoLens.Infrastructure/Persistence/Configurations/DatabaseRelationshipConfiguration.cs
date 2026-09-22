using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class DatabaseRelationshipConfiguration : IEntityTypeConfiguration<DatabaseRelationship>
{
    public void Configure(EntityTypeBuilder<DatabaseRelationship> builder)
    {
        builder.ToTable("database_relationships");

        builder.HasKey(r => r.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(r => r.AnalysisId);

        builder.Property(r => r.RelationshipType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(r => r.Analysis)
            .WithMany(a => a.DatabaseRelationships)
            .HasForeignKey(r => r.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        // TODO: [Giả định cần chốt với nhóm] DeleteBehavior.Restrict cho cả SourceEntity và TargetEntity
        // để ngăn ngừa lỗi cycle/multiple cascade paths trong RDBMS.
        builder.HasOne(r => r.SourceEntity)
            .WithMany()
            .HasForeignKey(r => r.SourceEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetEntity)
            .WithMany()
            .HasForeignKey(r => r.TargetEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Evidence)
            .WithMany()
            .HasForeignKey(r => r.EvidenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
