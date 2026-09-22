using RepoLens.Domain.Exceptions;

namespace RepoLens.Domain.ValueObjects;

public readonly record struct ConfidenceScore : IComparable<ConfidenceScore>
{
    public float Value { get; }

    public ConfidenceScore(float value)
    {
        if (float.IsNaN(value) || value < 0.0f || value > 1.0f)
        {
            throw new InvalidConfidenceScoreException(value);
        }

        Value = value;
    }

    public static ConfidenceScore Exact => new(1.0f);
    public static ConfidenceScore High => new(0.85f);
    public static ConfidenceScore Medium => new(0.65f);
    public static ConfidenceScore Low => new(0.35f);
    public static ConfidenceScore Zero => new(0.0f);

    public static ConfidenceScore From(float value) => new(value);

    public static implicit operator float(ConfidenceScore score) => score.Value;
    public static explicit operator ConfidenceScore(float value) => new(value);

    public int CompareTo(ConfidenceScore other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString("0.00");
}
