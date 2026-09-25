namespace RepoLens.Infrastructure.Common;

/// <summary>
/// Mathematical utilities and constants for vector operations and pgvector compatibility (T086).
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Required dimension size for text-embedding-3-small vectors in RepoLens (1536).
    /// </summary>
    public const int RequiredDimensions = 1536;

    /// <summary>
    /// Default top-K nearest neighbours to retrieve.
    /// </summary>
    public const int DefaultTopK = 5;

    /// <summary>
    /// Maximum allowed top-K retrieval limit to prevent unbounded memory allocation.
    /// </summary>
    public const int MaxTopK = 100;

    /// <summary>
    /// Calculates the cosine distance between two vectors: 1.0 - cosine_similarity.
    /// Range: [0.0, 2.0], where 0.0 means identical, 1.0 means orthogonal, 2.0 means opposite.
    /// Matches the PostgreSQL pgvector cosine distance operator (&lt;=&gt;).
    /// </summary>
    public static double CosineDistance(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
        {
            throw new ArgumentException($"Vector dimension mismatch: vector A has {a.Length} dimensions, vector B has {b.Length} dimensions.");
        }

        if (a.Length == 0)
        {
            throw new ArgumentException("Vectors cannot be empty.", nameof(a));
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            double valA = a[i];
            double valB = b[i];

            dotProduct += valA * valB;
            normA += valA * valA;
            normB += valB * valB;
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 1.0;
        }

        double similarity = dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        similarity = Math.Clamp(similarity, -1.0, 1.0);

        return 1.0 - similarity;
    }
}
