namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// Configuration options for vector embedding provider (T082).
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>
    /// Embedding provider type: "Deterministic" (default for local dev/testing), "OpenAi", "Ollama".
    /// </summary>
    public string Provider { get; set; } = "Deterministic";

    /// <summary>
    /// API key for the embedding provider (if applicable).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model name (e.g., "text-embedding-3-small").
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Base URL endpoint for API calls (optional).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Dimensionality of embedding vectors (must match pgvector configuration, default 1536).
    /// </summary>
    public int Dimensions { get; set; } = 1536;
}
