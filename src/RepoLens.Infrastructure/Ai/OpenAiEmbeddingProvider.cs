using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions.AI;

namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// OpenAI Embedding Provider (T082).
/// Calls OpenAI /v1/embeddings API using HttpClient.
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OpenAiEmbeddingProvider> _logger;

    public OpenAiEmbeddingProvider(
        HttpClient httpClient,
        IOptions<EmbeddingOptions> options,
        ILogger<OpenAiEmbeddingProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new EmbeddingOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return [];
        }

        var baseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? _options.BaseUrl.TrimEnd('/')
            : "https://api.openai.com/v1";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/embeddings");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var payload = new
        {
            model = _options.Model,
            input = inputs,
            dimensions = _options.Dimensions
        };

        var json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);

        var data = doc.RootElement.GetProperty("data");
        var embeddings = new List<float[]>(inputs.Count);

        foreach (var item in data.EnumerateArray())
        {
            var vectorElement = item.GetProperty("embedding");
            var vector = new float[vectorElement.GetArrayLength()];
            var i = 0;
            foreach (var val in vectorElement.EnumerateArray())
            {
                vector[i++] = val.GetSingle();
            }
            embeddings.Add(vector);
        }

        return embeddings;
    }
}
