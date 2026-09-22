using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class DatabaseEntityConfiguration : IEntityTypeConfiguration<DatabaseEntity>
{
    public void Configure(EntityTypeBuilder<DatabaseEntity> builder)
    {
        builder.ToTable("database_entities");

        builder.HasKey(d => d.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(d => d.AnalysisId);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(d => d.Analysis)
            .WithMany(a => a.DatabaseEntities)
            .HasForeignKey(d => d.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.SourceSymbol)
            .WithMany()
            .HasForeignKey(d => d.SourceSymbolId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
