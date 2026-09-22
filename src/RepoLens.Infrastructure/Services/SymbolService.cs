using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Symbols;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class SymbolService : ISymbolService
{
    private readonly RepoLensDbContext _context;

    public SymbolService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<SymbolDetailResponse?> GetSymbolDetailAsync(Guid analysisId, Guid symbolId, CancellationToken ct = default)
    {
        var symbol = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .FirstOrDefaultAsync(s => s.SourceFile.AnalysisId == analysisId && s.Id == symbolId, ct);

        if (symbol == null)
        {
            return null;
        }

        var symbolIdStr = symbol.Id.ToString();
        var dependencies = await _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId && d.SourceId == symbolIdStr)
            .ToListAsync(ct);

        var relationships = dependencies.Select(d => new SymbolRelationshipDto(
            Type: d.DependencyType.ToString(),
            TargetSymbolId: d.TargetId
        )).ToList();

        return new SymbolDetailResponse(
            Id: symbol.Id.ToString(),
            Name: symbol.Name,
            FullName: symbol.FullName,
            Type: symbol.SymbolType.ToString(),
            File: new SymbolFileRefDto(symbol.SourceFile.Id.ToString(), symbol.SourceFile.Path),
            StartLine: symbol.StartLine,
            EndLine: symbol.EndLine,
            Relationships: relationships
        );
    }
}
