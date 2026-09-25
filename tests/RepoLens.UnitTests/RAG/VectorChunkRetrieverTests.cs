using Microsoft.EntityFrameworkCore;
using RepoLens.Infrastructure.Common;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Services;

namespace RepoLens.UnitTests.RAG;

public class VectorChunkRetrieverTests
{
    private static RepoLensDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseInMemoryDatabase(databaseName: "RepoLens_VectorRetriever_" + Guid.NewGuid().ToString("N"))
            .Options;

        return new RepoLensDbContext(options);
    }

    [Fact]
    public async Task RetrieveSimilarChunksAsync_WhenAnalysisIdIsEmpty_ThrowsArgumentException()
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);
        var query = new float[VectorMath.RequiredDimensions];

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.Empty, query));

        Assert.Equal("analysisId", ex.ParamName);
    }

    [Fact]
    public async Task RetrieveSimilarChunksAsync_WhenQueryEmbeddingIsNull_ThrowsArgumentNullException()
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task RetrieveSimilarChunksAsync_WhenQueryEmbeddingIsEmpty_ThrowsArgumentException()
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.NewGuid(), []));

        Assert.Equal("queryEmbedding", ex.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(512)]
    [InlineData(768)]
    [InlineData(1535)]
    [InlineData(1537)]
    [InlineData(3072)]
    public async Task RetrieveSimilarChunksAsync_WhenQueryEmbeddingHasInvalidDimensions_ThrowsArgumentException(int dimensions)
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);
        var query = new float[dimensions];

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.NewGuid(), query));

        Assert.Equal("queryEmbedding", ex.ParamName);
        Assert.Contains("1536", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task RetrieveSimilarChunksAsync_WhenTopKIsZeroOrNegative_ThrowsArgumentOutOfRangeException(int topK)
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);
        var query = new float[VectorMath.RequiredDimensions];

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.NewGuid(), query, topK));

        Assert.Equal("topK", ex.ParamName);
    }

    [Fact]
    public async Task RetrieveSimilarChunksAsync_WhenTopKExceedsMaximum_ThrowsArgumentOutOfRangeException()
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);
        var query = new float[VectorMath.RequiredDimensions];

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.NewGuid(), query, topK: VectorMath.MaxTopK + 1));

        Assert.Equal("topK", ex.ParamName);
    }

    [Fact]
    public async Task RetrieveSimilarChunksAsync_WhenNonPostgresProvider_ThrowsNotSupportedException()
    {
        using var context = CreateInMemoryDbContext();
        var retriever = new VectorChunkRetriever(context);
        var query = new float[VectorMath.RequiredDimensions];

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            retriever.RetrieveSimilarChunksAsync(Guid.NewGuid(), query));

        Assert.Contains("PostgreSQL", ex.Message);
        Assert.Contains("pgvector", ex.Message);
    }

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VectorChunkRetriever(null!));
    }
}
