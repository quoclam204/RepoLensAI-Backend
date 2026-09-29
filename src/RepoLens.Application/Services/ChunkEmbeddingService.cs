using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.DTOs.Persistence;

namespace RepoLens.Application.Services;

/// <summary>
/// Generates embeddings for already-eligible document chunks (T085 / FR-009).
/// Relies on the existing T083 safety/masking boundary: chunks arrive from
/// DocumentChunkGenerator with masked content, and this service performs no
/// masking, no approval authorization, and no persistence. Vectors are validated
/// (count and 1536 dimensions) before assignment; invalid vectors are never persisted.
/// </summary>
public sealed class ChunkEmbeddingService : IChunkEmbeddingService
{
    private readonly IEmbeddingProvider _provider;
    private readonly ChunkEmbeddingOptions _options;

    public ChunkEmbeddingService(IEmbeddingProvider provider, ChunkEmbeddingOptions? options = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? ChunkEmbeddingOptions.Default;
    }

    /// <inheritdoc />
    public async Task<(AnalysisResultModel Result, ChunkEmbeddingResult Outcome)> PopulateEmbeddingsAsync(
        AnalysisResultModel result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var chunks = result.DocumentChunks;
        var eligibleIndexes = new List<int>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(chunks[i].Content))
            {
                eligibleIndexes.Add(i);
            }
        }

        if (eligibleIndexes.Count == 0)
        {
            return (result, new ChunkEmbeddingResult(
                ChunkEmbeddingStatus.NoEligibleChunks,
                EligibleCount: 0,
                EmbeddedCount: 0,
                Errors: []));
        }

        var batchSize = Math.Max(1, _options.MaxBatchSize);
        var assigned = new float[eligibleIndexes.Count][];
        var errors = new List<string>();
        var succeeded = 0;

        for (var start = 0; start < eligibleIndexes.Count; start += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var end = Math.Min(start + batchSize, eligibleIndexes.Count);
            var batchIndexes = eligibleIndexes.GetRange(start, end - start);
            var inputs = new List<string>(batchIndexes.Count);
            foreach (var chunkIndex in batchIndexes)
            {
                inputs.Add(chunks[chunkIndex].Content);
            }

            IReadOnlyList<float[]> vectors;
            try
            {
                vectors = await _provider.EmbedAsync(inputs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"Batch starting at eligible index {start}: provider threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (vectors.Count != inputs.Count)
            {
                errors.Add($"Batch starting at eligible index {start}: provider returned {vectors.Count} vectors for {inputs.Count} inputs.");
                continue;
            }

            var batchOk = true;
            for (var k = 0; k < vectors.Count; k++)
            {
                var vector = vectors[k];
                if (vector is null || vector.Length != _options.EmbeddingDimension)
                {
                    errors.Add($"Batch starting at eligible index {start}: vector {k} has invalid dimension {vector?.Length.ToString() ?? "null"} (expected {_options.EmbeddingDimension}).");
                    batchOk = false;
                    break;
                }
            }

            if (!batchOk)
            {
                continue;
            }

            for (var k = 0; k < vectors.Count; k++)
            {
                assigned[start + k] = vectors[k];
            }

            succeeded += vectors.Count;
        }

        if (succeeded == 0)
        {
            return (result, new ChunkEmbeddingResult(
                ChunkEmbeddingStatus.CompleteFailure,
                EligibleCount: eligibleIndexes.Count,
                EmbeddedCount: 0,
                Errors: errors.AsReadOnly()));
        }

        var updated = new List<DocumentChunkPersistenceModel>(chunks.Count);
        var assignedByChunkIndex = new Dictionary<int, float[]>(eligibleIndexes.Count);
        for (var e = 0; e < eligibleIndexes.Count; e++)
        {
            if (assigned[e] is not null)
            {
                assignedByChunkIndex[eligibleIndexes[e]] = assigned[e];
            }
        }

        for (var i = 0; i < chunks.Count; i++)
        {
            if (assignedByChunkIndex.TryGetValue(i, out var vector))
            {
                updated.Add(chunks[i] with { Embedding = vector });
            }
            else
            {
                updated.Add(chunks[i]);
            }
        }

        var status = succeeded == eligibleIndexes.Count
            ? ChunkEmbeddingStatus.AllSucceeded
            : ChunkEmbeddingStatus.PartialFailure;

        var output = new AnalysisResultModel
        {
            AnalysisId = result.AnalysisId,
            CurrentStage = result.CurrentStage,
            NewStatus = result.NewStatus,
            CommitHash = result.CommitHash,
            Projects = result.Projects,
            SourceFiles = result.SourceFiles,
            CodeSymbols = result.CodeSymbols,
            Evidences = result.Evidences,
            Dependencies = result.Dependencies,
            ApiEndpoints = result.ApiEndpoints,
            DatabaseEntities = result.DatabaseEntities,
            DatabaseRelationships = result.DatabaseRelationships,
            Issues = result.Issues,
            DocumentChunks = updated.AsReadOnly()
        };

        return (output, new ChunkEmbeddingResult(
            status,
            EligibleCount: eligibleIndexes.Count,
            EmbeddedCount: succeeded,
            Errors: errors.AsReadOnly()));
    }
}
