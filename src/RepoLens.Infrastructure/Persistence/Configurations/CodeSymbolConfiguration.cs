using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Entities;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public class CodeSymbolConfiguration : IEntityTypeConfiguration<CodeSymbol>
{
    public void Configure(EntityTypeBuilder<CodeSymbol> builder)
    {
        builder.ToTable("code_symbols");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.SourceFileId);
        builder.HasIndex(s => s.FullName);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.FullName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.SymbolType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.StartLine)
            .IsRequired();

        builder.Property(s => s.EndLine)
            .IsRequired();

        builder.HasOne(s => s.SourceFile)
            .WithMany(f => f.Symbols)
            .HasForeignKey(s => s.SourceFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
