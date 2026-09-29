using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Common;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Helper to validate that an Analysis exists and has successfully completed before querying sub-resources.
/// Throws <see cref="AnalysisNotFoundException"/> (404) or <see cref="AnalysisNotReadyException"/> / <see cref="AnalysisFailedException"/> (409).
/// </summary>
internal static class AnalysisValidationHelper
{
    public static async Task EnsureAnalysisCompletedAsync(
        RepoLensDbContext context,
        Guid analysisId,
        CancellationToken ct = default)
    {
        var analysis = await context.Analyses
            .AsNoTracking()
            .Select(a => new { a.Id, a.Status, a.CurrentStage })
            .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

        if (analysis == null)
        {
            throw new AnalysisNotFoundException(analysisId);
        }

        if (analysis.Status == AnalysisStatus.Failed)
        {
            throw new AnalysisFailedException(analysis.CurrentStage);
        }

        if (analysis.Status != AnalysisStatus.Completed)
        {
            throw new AnalysisNotReadyException(analysis.Status.ToString());
        }
    }
}
