using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class ApiEndpointConfiguration : IEntityTypeConfiguration<ApiEndpoint>
{
    public void Configure(EntityTypeBuilder<ApiEndpoint> builder)
    {
        builder.ToTable("api_endpoints");

        builder.HasKey(e => e.Id);

        // NFR-004 Repository Isolation: Index on AnalysisId
        builder.HasIndex(e => e.AnalysisId);
        builder.HasIndex(e => e.ProjectId);

        builder.Property(e => e.Method)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Route)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Controller)
            .HasMaxLength(200);

        builder.Property(e => e.Action)
            .HasMaxLength(200);

        builder.HasOne(e => e.Analysis)
            .WithMany(a => a.ApiEndpoints)
            .HasForeignKey(e => e.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Project)
            .WithMany(p => p.ApiEndpoints)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Symbol)
            .WithMany()
            .HasForeignKey(e => e.SymbolId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Evidence)
            .WithMany()
            .HasForeignKey(e => e.EvidenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
