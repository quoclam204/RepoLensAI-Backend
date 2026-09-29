using System.Text.RegularExpressions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;

namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// Deterministic AI generation provider for local development, CI testing, and evaluation (T081).
/// Formulates grounded answers strictly from the provided context without making external API calls.
/// If context is missing or contains no repository evidence, returns the canonical insufficient-evidence response.
/// </summary>
public sealed class DeterministicAiProvider : IAiProvider
{
    private static readonly Regex FileHeaderRegex = new(@"--- File:\s*([^\r\n]+)\s*---", RegexOptions.Compiled);

    /// <inheritdoc />
    public Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var context = request.Context ?? string.Empty;

        // Check if context has any evidence chunks
        if (string.IsNullOrWhiteSpace(context) || !context.Contains("--- File:"))
        {
            return Task.FromResult(new AiResponse
            {
                Answer = "Insufficient evidence in the analyzed repository",
                Confidence = AiConfidenceLevel.Unknown,
                Evidence = []
            });
        }

        // Extract referenced files from context
        var matches = FileHeaderRegex.Matches(context);
        var evidenceList = new List<AiEvidenceItem>();

        foreach (Match match in matches)
        {
            var fileName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(fileName) && !evidenceList.Any(e => e.File == fileName))
            {
                evidenceList.Add(new AiEvidenceItem
                {
                    File = fileName,
                    Reason = $"Directly matched in repository source evidence for query: {request.Prompt}"
                });
            }
        }

        var filesSummary = string.Join(", ", evidenceList.Select(e => e.File));
        var answer = $"Based on the repository evidence in {filesSummary}, the implementation addresses: {request.Prompt}";

        return Task.FromResult(new AiResponse
        {
            Answer = answer,
            Confidence = AiConfidenceLevel.High,
            Evidence = evidenceList
        });
    }
}
