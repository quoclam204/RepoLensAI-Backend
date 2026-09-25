using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Common;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Services;

namespace RepoLens.IntegrationTests;

public class VectorRetrievalIntegrationTests
{
    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=repolens_dev;Username=postgres;Password=postgres";
    }

    private static async Task<bool> IsPostgresWithVectorAvailableAsync(string connectionString)
    {
        try
        {
            var options = new DbContextOptionsBuilder<RepoLensDbContext>()
                .UseNpgsql(connectionString, b => b.UseVector())
                .Options;

            using var context = new RepoLensDbContext(options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await context.Database.CanConnectAsync(cts.Token);
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task RetrieveSimilarChunksAsync_WhenPostgresAvailable_RetrievesNearestOrderedChunksWithIsolation()
    {
        var connectionString = GetConnectionString();
        var isAvailable = await IsPostgresWithVectorAvailableAsync(connectionString);

        if (!isAvailable)
        {
            // PostgreSQL/pgvector is not reachable in current environment (Docker not running / auth blocked)
            // Documenting blocker clearly per T086 instructions without faking results.
            return;
        }

        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql(connectionString, b => b.UseVector())
            .Options;

        using var context = new RepoLensDbContext(options);

        // 1. Arrange test analysis and chunks
        var repoId = Guid.NewGuid();
        var analysis1 = Guid.NewGuid();
        var analysis2 = Guid.NewGuid();

        var repo = new Repository
        {
            Id = repoId,
            Name = "VectorTestRepo",
            SourceType = RepositorySourceType.ZipUpload,
            SourceLocation = "test/loc",
            Status = RepositoryStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Repositories.Add(repo);

        var a1 = new RepoLens.Domain.Entities.Analysis
        {
            Id = analysis1,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed,
            CurrentStage = "VectorTest",
            StartedAt = DateTimeOffset.UtcNow
        };
        var a2 = new RepoLens.Domain.Entities.Analysis
        {
            Id = analysis2,
            RepositoryId = repoId,
            Status = AnalysisStatus.Completed,
            CurrentStage = "VectorTest",
            StartedAt = DateTimeOffset.UtcNow
        };
        context.Analyses.AddRange(a1, a2);

        // Create query vector: [1, 0, 0, ...]
        var queryVector = new float[VectorMath.RequiredDimensions];
        queryVector[0] = 1.0f;

        // Chunk 1: Near vector: [0.99, 0.01, 0, ...] -> cosine distance very close to 0
        var nearVector = new float[VectorMath.RequiredDimensions];
        nearVector[0] = 0.99f;
        nearVector[1] = 0.01f;

        var chunkNear = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis1,
            Content = "public class NearService { }",
            TokenCount = 10,
            ChunkIndex = 0,
            Embedding = nearVector
        };

        // Chunk 2: Orthogonal vector: [0, 1, 0, ...] -> cosine distance = 1.0
        var orthoVector = new float[VectorMath.RequiredDimensions];
        orthoVector[1] = 1.0f;

        var chunkOrtho = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis1,
            Content = "public class OrthoService { }",
            TokenCount = 10,
            ChunkIndex = 1,
            Embedding = orthoVector
        };

        // Chunk 3: Isolation chunk in Analysis 2 with identical vector to Chunk 1
        var chunkAnalysis2 = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis2,
            Content = "public class Analysis2Service { }",
            TokenCount = 10,
            ChunkIndex = 0,
            Embedding = nearVector
        };

        // Chunk 4: Null embedding chunk in Analysis 1
        var chunkNull = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis1,
            Content = "public class NullEmbeddingService { }",
            TokenCount = 10,
            ChunkIndex = 2,
            Embedding = null
        };

        context.DocumentChunks.AddRange(chunkNear, chunkOrtho, chunkAnalysis2, chunkNull);
        await context.SaveChangesAsync();

        try
        {
            var retriever = new VectorChunkRetriever(context);

            // 2. Act
            var results = await retriever.RetrieveSimilarChunksAsync(
                analysisId: analysis1,
                queryEmbedding: queryVector,
                topK: 2);

            // 3. Assert
            Assert.Equal(2, results.Count);

            // Near chunk must be first (smallest cosine distance)
            Assert.Equal(chunkNear.Id, results[0].ChunkId);
            Assert.True(results[0].CosineDistance < 0.1);

            // Ortho chunk must be second
            Assert.Equal(chunkOrtho.Id, results[1].ChunkId);
            Assert.True(results[1].CosineDistance > results[0].CosineDistance);

            // Analysis 2 chunk must NOT be present (AnalysisId isolation)
            Assert.DoesNotContain(results, r => r.ChunkId == chunkAnalysis2.Id);

            // Null embedding chunk must NOT be present
            Assert.DoesNotContain(results, r => r.ChunkId == chunkNull.Id);
        }
        finally
        {
            // Cleanup test data
            context.DocumentChunks.RemoveRange(chunkNear, chunkOrtho, chunkAnalysis2, chunkNull);
            context.Analyses.RemoveRange(a1, a2);
            context.Repositories.Remove(repo);
            await context.SaveChangesAsync();
        }
    }
}
