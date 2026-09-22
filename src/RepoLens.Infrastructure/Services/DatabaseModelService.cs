using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Database;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class DatabaseModelService : IDatabaseModelService
{
    private readonly RepoLensDbContext _context;

    public DatabaseModelService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<DatabaseModelResponse?> GetDatabaseModelAsync(Guid analysisId, CancellationToken ct = default)
    {
        var entities = await _context.DatabaseEntities
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId)
            .ToListAsync(ct);

        var relationships = await _context.DatabaseRelationships
            .AsNoTracking()
            .Where(r => r.AnalysisId == analysisId)
            .ToListAsync(ct);

        // TODO: [Giả định cần chốt với nhóm] DatabaseEntity trong Domain hiện chưa có bảng con lưu Properties.
        // Tạm thời trả về mảng rỗng và sẽ mở rộng trích xuất từ CodeSymbol hoặc mở rộng Entity ở Phase sau.
        var entityDtos = entities.Select(e => new DatabaseEntityDto(
            Id: e.Id.ToString(),
            Name: e.Name,
            Type: e.EntityType,
            SourceSymbolId: e.SourceSymbolId?.ToString(),
            Properties: Array.Empty<EntityPropertyDto>()
        )).ToList();

        var relationshipDtos = relationships.Select(r => new DatabaseRelationshipDto(
            Id: r.Id.ToString(),
            SourceEntityId: r.SourceEntityId.ToString(),
            TargetEntityId: r.TargetEntityId.ToString(),
            Type: r.RelationshipType.ToString(),
            Confidence: r.EvidenceId.HasValue ? "confirmed" : "inferred",
            EvidenceId: r.EvidenceId?.ToString()
        )).ToList();

        return new DatabaseModelResponse(entityDtos, relationshipDtos);
    }

    public async Task<DatabaseEntityDetailResponse?> GetEntityDetailAsync(Guid analysisId, Guid entityId, CancellationToken ct = default)
    {
        var entity = await _context.DatabaseEntities
            .AsNoTracking()
            .Include(e => e.SourceSymbol)
                .ThenInclude(s => s!.SourceFile)
            .FirstOrDefaultAsync(e => e.AnalysisId == analysisId && e.Id == entityId, ct);

        if (entity == null)
        {
            return null;
        }

        var relationships = await _context.DatabaseRelationships
            .AsNoTracking()
            .Where(r => r.AnalysisId == analysisId && (r.SourceEntityId == entityId || r.TargetEntityId == entityId))
            .ToListAsync(ct);

        var relDtos = relationships.Select(r => new DatabaseRelationshipDto(
            Id: r.Id.ToString(),
            SourceEntityId: r.SourceEntityId.ToString(),
            TargetEntityId: r.TargetEntityId.ToString(),
            Type: r.RelationshipType.ToString(),
            Confidence: r.EvidenceId.HasValue ? "confirmed" : "inferred",
            EvidenceId: r.EvidenceId?.ToString()
        )).ToList();

        DatabaseSourceRefDto? sourceRef = null;
        if (entity.SourceSymbol != null && entity.SourceSymbol.SourceFile != null)
        {
            sourceRef = new DatabaseSourceRefDto(
                entity.SourceSymbol.SourceFile.Path,
                entity.SourceSymbol.FullName);
        }

        // Query evidence associated with this database entity
        var evidences = await _context.Evidences
            .AsNoTracking()
            .Where(ev => ev.AnalysisId == analysisId && (ev.Symbol == entity.Name || (sourceRef != null && ev.FilePath == sourceRef.File)))
            .ToListAsync(ct);

        var evidenceDtos = evidences.Select(ev => new DatabaseEvidenceSnippetDto(
            ev.FilePath,
            ev.StartLine,
            ev.EndLine,
            !string.IsNullOrWhiteSpace(ev.Description) ? ev.Description : $"Defines the {entity.Name} entity."
        )).ToList();

        return new DatabaseEntityDetailResponse(
            Id: entity.Id.ToString(),
            Name: entity.Name,
            Type: entity.EntityType,
            Source: sourceRef,
            Properties: Array.Empty<EntityPropertyDto>(),
            Relationships: relDtos,
            Evidence: evidenceDtos
        );
    }
}
