using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;

namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// OpenAI generation provider (T081).
/// Calls OpenAI /v1/chat/completions API using HttpClient.
/// </summary>
public sealed class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new AiOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? _options.BaseUrl.TrimEnd('/')
            : "https://api.openai.com/v1";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }

        var userContent = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            userContent.AppendLine("CONTEXT EVIDENCE:");
            userContent.AppendLine(request.Context);
            userContent.AppendLine();
        }
        userContent.AppendLine("QUESTION:");
        userContent.AppendLine(request.Prompt);

        messages.Add(new { role = "user", content = userContent.ToString() });

        var payload = new
        {
            model = _options.Model,
            messages = messages,
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(payload);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);

        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            return new AiResponse
            {
                Answer = "Insufficient evidence in the analyzed repository",
                Confidence = AiConfidenceLevel.Unknown,
                Evidence = []
            };
        }

        var message = choices[0].GetProperty("message");
        var content = message.GetProperty("content").GetString() ?? string.Empty;

        return new AiResponse
        {
            Answer = content,
            Confidence = AiConfidenceLevel.Medium,
            Evidence = []
        };
    }
}
