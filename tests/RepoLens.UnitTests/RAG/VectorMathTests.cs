using RepoLens.Infrastructure.Common;

namespace RepoLens.UnitTests.RAG;

public class VectorMathTests
{
    [Fact]
    public void CosineDistance_IdenticalVectors_ReturnsZero()
    {
        var v1 = new float[] { 1f, 2f, 3f };
        var v2 = new float[] { 1f, 2f, 3f };

        var distance = VectorMath.CosineDistance(v1, v2);

        Assert.Equal(0.0, distance, precision: 5);
    }

    [Fact]
    public void CosineDistance_ScaledParallelVectors_ReturnsZero()
    {
        var v1 = new float[] { 1f, 2f, 3f };
        var v2 = new float[] { 2f, 4f, 6f };

        var distance = VectorMath.CosineDistance(v1, v2);

        Assert.Equal(0.0, distance, precision: 5);
    }

    [Fact]
    public void CosineDistance_OrthogonalVectors_ReturnsOne()
    {
        var v1 = new float[] { 1f, 0f, 0f };
        var v2 = new float[] { 0f, 1f, 0f };

        var distance = VectorMath.CosineDistance(v1, v2);

        Assert.Equal(1.0, distance, precision: 5);
    }

    [Fact]
    public void CosineDistance_OppositeVectors_ReturnsTwo()
    {
        var v1 = new float[] { 1f, 2f, 3f };
        var v2 = new float[] { -1f, -2f, -3f };

        var distance = VectorMath.CosineDistance(v1, v2);

        Assert.Equal(2.0, distance, precision: 5);
    }

    [Fact]
    public void CosineDistance_ZeroVector_ReturnsOne()
    {
        var v1 = new float[] { 0f, 0f, 0f };
        var v2 = new float[] { 1f, 2f, 3f };

        var distance = VectorMath.CosineDistance(v1, v2);

        Assert.Equal(1.0, distance, precision: 5);
    }

    [Fact]
    public void CosineDistance_DimensionMismatch_ThrowsArgumentException()
    {
        var v1 = new float[] { 1f, 2f };
        var v2 = new float[] { 1f, 2f, 3f };

        Assert.Throws<ArgumentException>(() => VectorMath.CosineDistance(v1, v2));
    }

    [Fact]
    public void CosineDistance_NullVectors_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VectorMath.CosineDistance(null!, new float[] { 1f }));
        Assert.Throws<ArgumentNullException>(() => VectorMath.CosineDistance(new float[] { 1f }, null!));
    }

    [Fact]
    public void CosineDistance_EmptyVectors_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => VectorMath.CosineDistance([], []));
    }
}
