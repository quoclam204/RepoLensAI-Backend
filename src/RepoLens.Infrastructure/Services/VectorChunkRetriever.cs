using Microsoft.EntityFrameworkCore;
using Pgvector;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Models.RAG;
using RepoLens.Infrastructure.Common;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// PostgreSQL pgvector implementation of vector similarity retrieval for document chunks (T086).
/// Uses cosine distance operator (&lt;=&gt;) in parameterized SQL to locate the most relevant
/// chunks strictly scoped to the specified analysis run.
/// </summary>
public class VectorChunkRetriever : IVectorChunkRetriever
{
    private readonly RepoLensDbContext _context;

    public VectorChunkRetriever(RepoLensDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
        Guid analysisId,
        float[] queryEmbedding,
        int topK = VectorMath.DefaultTopK,
        CancellationToken cancellationToken = default)
    {
        // 1. Input validation
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (queryEmbedding.Length == 0)
        {
            throw new ArgumentException("Query embedding cannot be empty.", nameof(queryEmbedding));
        }

        if (queryEmbedding.Length != VectorMath.RequiredDimensions)
        {
            throw new ArgumentException(
                $"Query embedding must have exactly {VectorMath.RequiredDimensions} dimensions, but got {queryEmbedding.Length}.",
                nameof(queryEmbedding));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be greater than zero.");
        }

        if (topK > VectorMath.MaxTopK)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), $"TopK cannot exceed {VectorMath.MaxTopK}.");
        }

        // 2. Database provider check
        if (!_context.Database.IsNpgsql())
        {
            throw new NotSupportedException(
                "Vector similarity retrieval requires a PostgreSQL database with the pgvector extension enabled.");
        }

        // 3. PostgreSQL pgvector parameterized query execution
        var pgVector = new Vector(queryEmbedding);

        var chunks = await _context.DocumentChunks
            .FromSqlInterpolated($@"
                SELECT *
                FROM document_chunks
                WHERE ""AnalysisId"" = {analysisId}
                  AND ""Embedding"" IS NOT NULL
                ORDER BY ""Embedding"" <=> {pgVector}
                LIMIT {topK}")
            .AsNoTracking()
            .Include(c => c.SourceFile)
            .Include(c => c.Evidence)
            .ToListAsync(cancellationToken);

        // 4. Map to search results with cosine distance and similarity metrics
        var results = new List<VectorChunkSearchResult>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var distance = chunk.Embedding != null
                ? VectorMath.CosineDistance(queryEmbedding, chunk.Embedding)
                : 1.0;
            var similarity = 1.0 - distance;
            var confidence = chunk.Evidence?.Confidence?.Value ?? 1.0f;

            results.Add(new VectorChunkSearchResult(
                ChunkId: chunk.Id,
                AnalysisId: chunk.AnalysisId,
                SourceFileId: chunk.SourceFileId,
                FilePath: chunk.SourceFile?.Path ?? chunk.Evidence?.FilePath ?? string.Empty,
                Symbol: chunk.Evidence?.Symbol,
                StartLine: chunk.Evidence?.StartLine ?? 1,
                EndLine: chunk.Evidence?.EndLine ?? 1,
                Content: chunk.Content,
                TokenCount: chunk.TokenCount,
                ChunkIndex: chunk.ChunkIndex,
                EvidenceId: chunk.EvidenceId,
                ConfidenceScore: confidence,
                CosineDistance: distance,
                SimilarityScore: similarity));
        }

        return results.AsReadOnly();
    }
}
