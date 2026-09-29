namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// Configuration options for the AI generation provider (T081).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// AI provider type: "Deterministic" (default for local dev/testing), "OpenAi", "Ollama".
    /// </summary>
    public string Provider { get; set; } = "Deterministic";

    /// <summary>
    /// API key for the AI provider (if applicable).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model name (e.g., "gpt-4o-mini", "llama3").
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Base URL endpoint for API calls (optional, for custom endpoints or Ollama).
    /// </summary>
    public string? BaseUrl { get; set; }
}
