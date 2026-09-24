using RepoLens.Domain.Exceptions;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.UnitTests.Domain;

public class ConfidenceScoreTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(0.85f)]
    [InlineData(1.0f)]
    public void Constructor_WithValidValue_CreatesInstance(float validScore)
    {
        var score = new ConfidenceScore(validScore);

        Assert.Equal(validScore, score.Value);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(-1.0f)]
    [InlineData(1.01f)]
    [InlineData(2.5f)]
    [InlineData(float.NaN)]
    public void Constructor_WithOutOfRangeValue_ThrowsInvalidConfidenceScoreException(float invalidScore)
    {
        var ex = Assert.Throws<InvalidConfidenceScoreException>(() => new ConfidenceScore(invalidScore));
        Assert.Equal(invalidScore, ex.Value);
    }

    [Fact]
    public void StaticHelpers_ProduceExpectedValues()
    {
        Assert.Equal(1.0f, ConfidenceScore.Exact.Value);
        Assert.Equal(0.85f, ConfidenceScore.High.Value);
        Assert.Equal(0.65f, ConfidenceScore.Medium.Value);
        Assert.Equal(0.35f, ConfidenceScore.Low.Value);
        Assert.Equal(0.0f, ConfidenceScore.Zero.Value);
    }

    [Fact]
    public void ComparisonAndImplicitCast_WorkAsExpected()
    {
        ConfidenceScore high = ConfidenceScore.High;
        ConfidenceScore low = ConfidenceScore.Low;

        Assert.True(high > low);
        float floatVal = high;
        Assert.Equal(0.85f, floatVal);
    }
}
