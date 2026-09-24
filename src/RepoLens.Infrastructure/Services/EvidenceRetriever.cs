using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Models.RAG;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Service implementing grounded evidence retrieval from persisted static analysis knowledge models,
/// strictly enforcing that answers can only be generated when backed by verified source code locations.
/// </summary>
public class EvidenceRetriever : IEvidenceRetriever
{
    private readonly RepoLensDbContext _context;

    public EvidenceRetriever(RepoLensDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<GroundedRetrievalResult> RetrieveGroundedEvidenceAsync(
        Guid analysisId,
        string question,
        string? targetSymbolOrPath = null,
        float minimumConfidence = 0.5f,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var query = _context.Evidences
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId);

        if (!string.IsNullOrWhiteSpace(targetSymbolOrPath))
        {
            var search = targetSymbolOrPath.Trim();
            query = query.Where(e =>
                e.FilePath.Contains(search) ||
                (e.Symbol != null && e.Symbol.Contains(search)) ||
                e.Description.Contains(search));
        }

        var evidences = await query
            .OrderBy(e => e.FilePath)
            .ThenBy(e => e.StartLine)
            .Take(20)
            .ToListAsync(cancellationToken);

        var retrievedItems = evidences.Select(e => new RetrievedEvidenceItem(
            EvidenceId: e.Id,
            FilePath: e.FilePath,
            StartLine: e.StartLine,
            EndLine: e.EndLine,
            Snippet: e.Description,
            EvidenceType: e.EvidenceType.ToString(),
            ConfidenceScore: e.Confidence?.Value ?? 1.0f,
            Symbol: e.Symbol)).ToList();

        var qualifyingItems = retrievedItems.Where(i => i.ConfidenceScore >= minimumConfidence).ToList();

        if (qualifyingItems.Count == 0)
        {
            return new GroundedRetrievalResult(
                Question: question,
                Items: [],
                HasSufficientEvidence: false,
                ConfidenceLevel: "Zero",
                GroundingStatus: "Insufficient evidence");
        }

        var avgConfidence = qualifyingItems.Average(i => i.ConfidenceScore);
        var confidenceLevel = avgConfidence switch
        {
            >= 0.8f => "High",
            >= 0.5f => "Medium",
            _ => "Low"
        };

        return new GroundedRetrievalResult(
            Question: question,
            Items: qualifyingItems.AsReadOnly(),
            HasSufficientEvidence: true,
            ConfidenceLevel: confidenceLevel,
            GroundingStatus: "Grounded");
    }

    public async Task<IReadOnlyList<RetrievedChunkItem>> RetrieveDocumentChunksAsync(
        Guid analysisId,
        string? targetSymbolOrPath = null,
        string? keyword = null,
        float minimumConfidence = 0.5f,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Evidence)
            .Include(c => c.SourceFile)
            .Where(c => c.AnalysisId == analysisId);

        if (!string.IsNullOrWhiteSpace(targetSymbolOrPath))
        {
            var search = targetSymbolOrPath.Trim();
            query = query.Where(c =>
                (c.SourceFile != null && c.SourceFile.Path.Contains(search)) ||
                (c.Evidence != null && c.Evidence.FilePath.Contains(search)) ||
                (c.Evidence != null && c.Evidence.Symbol != null && c.Evidence.Symbol.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(c => c.Content.Contains(kw));
        }

        var chunks = await query
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

        var result = new List<RetrievedChunkItem>();
        foreach (var c in chunks)
        {
            var confidence = c.Evidence?.Confidence?.Value ?? 1.0f;
            if (confidence < minimumConfidence)
            {
                continue;
            }

            result.Add(new RetrievedChunkItem(
                ChunkId: c.Id,
                AnalysisId: c.AnalysisId,
                SourceFileId: c.SourceFileId,
                FilePath: c.SourceFile?.Path ?? c.Evidence?.FilePath ?? string.Empty,
                Symbol: c.Evidence?.Symbol,
                StartLine: c.Evidence?.StartLine ?? 1,
                EndLine: c.Evidence?.EndLine ?? 1,
                Content: c.Content,
                TokenCount: c.TokenCount,
                ChunkIndex: c.ChunkIndex,
                EvidenceId: c.EvidenceId,
                ConfidenceScore: confidence));
        }

        return result.AsReadOnly();
    }
}
