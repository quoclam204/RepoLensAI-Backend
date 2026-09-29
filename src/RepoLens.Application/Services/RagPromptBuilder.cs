using System.Globalization;
using System.Text;
using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Services;

/// <summary>
/// Deterministic prompt and context builder for evidence-grounded RAG (T087).
/// Strictly enforces that the AI model treats repository evidence as the sole ground truth.
/// </summary>
public static class RagPromptBuilder
{
    public const string DefaultSystemPrompt =
        "You are RepoLens AI, an evidence-grounded software architecture and codebase intelligence assistant.\n" +
        "Your primary directive is: Static analysis establishes what actually exists. AI explains the evidence.\n\n" +
        "Strict Grounding Rules:\n" +
        "1. The provided repository evidence chunks are your sole source of truth.\n" +
        "2. You must answer based ONLY on the supplied repository evidence.\n" +
        "3. Do NOT fabricate, extrapolate, or hallucinate files, classes, methods, symbols, endpoints, database schemas, or dependencies that are not explicitly present in the evidence.\n" +
        "4. If the provided evidence is missing, empty, or insufficient to answer the question, explicitly state that the repository evidence does not contain this information rather than guessing or assuming.\n" +
        "5. Cite the exact file paths, line numbers, and symbols from the evidence chunks when referencing code or making architectural claims.\n" +
        "6. Do not present assumptions or external knowledge as verified facts about this repository.";

    public static string BuildContext(IReadOnlyList<VectorChunkSearchResult> chunks, Guid analysisId)
    {
        if (chunks == null || chunks.Count == 0)
        {
            return $"--- REPOSITORY EVIDENCE CHUNKS ---\nNo relevant repository evidence chunks were found for this query in analysis run {analysisId}.\n-----------------------------------";
        }

        var sb = new StringBuilder();
        sb.AppendLine("--- REPOSITORY EVIDENCE CHUNKS ---");
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            sb.AppendLine($"[Chunk {i + 1}]");
            sb.AppendLine($"File: {chunk.FilePath}");
            sb.AppendLine($"Lines: {chunk.StartLine}-{chunk.EndLine}");
            if (!string.IsNullOrWhiteSpace(chunk.Symbol))
            {
                sb.AppendLine($"Symbol: {chunk.Symbol}");
            }
            sb.AppendLine(CultureInfo.InvariantCulture, $"Similarity Score: {(chunk.SimilarityScore * 100):F1}%");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Confidence: {chunk.ConfidenceScore:F2}");
            sb.AppendLine("Content:");
            sb.AppendLine(chunk.Content);
            sb.AppendLine("-----------------------------------");
        }

        return sb.ToString().TrimEnd();
    }
}
