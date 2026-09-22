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
}
