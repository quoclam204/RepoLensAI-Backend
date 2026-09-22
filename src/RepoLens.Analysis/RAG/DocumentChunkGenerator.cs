using RepoLens.Analysis.Graph;

namespace RepoLens.Analysis.RAG;

/// <summary>
/// Descriptor representing an evidence-grounded source code chunk for RAG embedding and vector indexing.
/// </summary>
public sealed record GeneratedDocumentChunk(
    string FilePath,
    string? Symbol,
    int StartLine,
    int EndLine,
    string Content,
    int TokenCount,
    int ChunkIndex,
    string? EvidenceKey);

/// <summary>
/// Generates deterministic, symbol-aware document chunks from source files and knowledge nodes
/// to serve as the grounded foundation for vector embedding and RAG retrieval.
/// </summary>
public static class DocumentChunkGenerator
{
    private const int ApproxCharsPerToken = 4;
    private const int MaxChunkChars = 2000;

    /// <summary>
    /// Generates document chunks based on symbol boundaries (classes, methods, endpoints) and file content.
    /// </summary>
    public static IReadOnlyList<GeneratedDocumentChunk> GenerateChunks(
        IReadOnlyList<KnowledgeNode> nodes,
        IReadOnlyDictionary<string, string> fileContents)
    {
        var chunks = new List<GeneratedDocumentChunk>();
        var chunkIndex = 0;

        // Group code symbols by file path
        var symbolsByFile = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.FilePath) && n.Location != null)
            .GroupBy(n => n.FilePath, StringComparer.OrdinalIgnoreCase);

        var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileGroup in symbolsByFile)
        {
            var filePath = fileGroup.Key!;
            processedFiles.Add(filePath);

            fileContents.TryGetValue(filePath, out var rawContent);
            var fileLines = rawContent != null ? rawContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None) : Array.Empty<string>();

            // Generate chunks for high-value symbols (classes, interfaces, methods, endpoints)
            var symbols = fileGroup
                .OrderBy(s => s.Location!.Value.StartLine)
                .ToList();

            foreach (var symbol in symbols)
            {
                var startLine = symbol.Location!.Value.StartLine;
                var endLine = symbol.Location!.Value.EndLine;

                string chunkContent;
                if (fileLines.Length > 0 && startLine <= fileLines.Length)
                {
                    var actualEnd = Math.Min(endLine, fileLines.Length);
                    var actualStart = Math.Max(1, startLine);
                    var count = actualEnd - actualStart + 1;
                    var lines = fileLines.Skip(actualStart - 1).Take(count);
                    chunkContent = string.Join("\n", lines);
                }
                else
                {
                    chunkContent = $"Symbol: {symbol.Name} ({symbol.Type}) at {filePath}:{startLine}-{endLine}";
                }

                if (chunkContent.Length > MaxChunkChars)
                {
                    chunkContent = string.Concat(chunkContent.AsSpan(0, MaxChunkChars - 3), "...");
                }

                var tokenCount = Math.Max(1, chunkContent.Length / ApproxCharsPerToken);
                var evidenceKey = $"ev:{filePath}:{startLine}-{endLine}";

                chunks.Add(new GeneratedDocumentChunk(
                    FilePath: filePath,
                    Symbol: symbol.Name,
                    StartLine: startLine,
                    EndLine: endLine,
                    Content: chunkContent,
                    TokenCount: tokenCount,
                    ChunkIndex: chunkIndex++,
                    EvidenceKey: evidenceKey));
            }
        }

        // For files without individual extracted symbols (e.g. scripts or configs), generate whole-file or windowed chunks
        foreach (var (filePath, content) in fileContents)
        {
            if (processedFiles.Contains(filePath) || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var tokenCount = Math.Max(1, content.Length / ApproxCharsPerToken);
            var snippet = content.Length > MaxChunkChars
                ? string.Concat(content.AsSpan(0, MaxChunkChars - 3), "...")
                : content;

            var lineCount = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length;

            chunks.Add(new GeneratedDocumentChunk(
                FilePath: filePath,
                Symbol: null,
                StartLine: 1,
                EndLine: Math.Max(1, lineCount),
                Content: snippet,
                TokenCount: tokenCount,
                ChunkIndex: chunkIndex++,
                EvidenceKey: $"ev:{filePath}:1-{Math.Max(1, lineCount)}"));
        }

        return chunks.AsReadOnly();
    }
}
