using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Security;
using RepoLens.Domain.Entities;
using DomainEvidence = RepoLens.Domain.Entities.Evidence;

namespace RepoLens.Analysis.RAG;

/// <summary>
/// Configuration options for document and source code chunking (T083 / FR-009).
/// </summary>
public sealed record DocumentChunkOptions
{
    public static DocumentChunkOptions Default { get; } = new();

    public int MaxChunkChars { get; init; } = 2000;
    public int ApproxCharsPerToken { get; init; } = 4;
    public int OverlapLines { get; init; } = 0;
}

/// <summary>
/// Descriptor representing an evidence-grounded source code chunk for RAG embedding and vector indexing.
/// Every chunk preserves complete source location and evidence traceability.
/// </summary>
public sealed record GeneratedDocumentChunk(
    string FilePath,
    string? Symbol,
    int StartLine,
    int EndLine,
    string Content,
    int TokenCount,
    int ChunkIndex,
    string? EvidenceKey,
    IReadOnlyList<string>? EvidenceKeys = null,
    float ConfidenceScore = 1.0f,
    IReadOnlyList<Guid>? EvidenceIds = null)
{
    public IReadOnlyList<string> AllEvidenceKeys =>
        EvidenceKeys ?? (EvidenceKey != null ? [EvidenceKey] : []);

    public IReadOnlyList<Guid> AllEvidenceIds =>
        EvidenceIds ?? [];
}

/// <summary>
/// Generates deterministic, symbol-aware document chunks from source files, documentation, and knowledge nodes
/// while strictly preserving evidence metadata, provenance, and applying secret masking (FR-009 / T083).
/// </summary>
public static class DocumentChunkGenerator
{
    /// <summary>
    /// Generates document chunks based on symbol boundaries and file content.
    /// Backwards-compatible signature.
    /// </summary>
    public static IReadOnlyList<GeneratedDocumentChunk> GenerateChunks(
        IReadOnlyList<KnowledgeNode> nodes,
        IReadOnlyDictionary<string, string> fileContents)
    {
        return GenerateChunks(nodes, fileContents, existingEvidences: null, options: null);
    }

    /// <summary>
    /// Generates deterministic, evidence-preserved document chunks from knowledge nodes, file content,
    /// and existing Domain Evidences (T083 / FR-009).
    /// </summary>
    public static IReadOnlyList<GeneratedDocumentChunk> GenerateChunks(
        IReadOnlyList<KnowledgeNode> nodes,
        IReadOnlyDictionary<string, string> fileContents,
        IReadOnlyList<DomainEvidence>? existingEvidences,
        DocumentChunkOptions? options = null)
    {
        var opts = options ?? DocumentChunkOptions.Default;
        var chunks = new List<GeneratedDocumentChunk>();
        var chunkIndex = 0;

        // Group code symbols by file path
        var symbolsByFile = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.FilePath) && n.Location != null)
            .GroupBy(n => n.FilePath!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Group existing evidences by file path for fast O(1) file lookup
        var evidencesByFile = (existingEvidences ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e.FilePath))
            .GroupBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Deterministic file traversal: sort file paths alphabetically
        var allFiles = fileContents.Keys
            .Concat(symbolsByFile.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var filePath in allFiles)
        {
            fileContents.TryGetValue(filePath, out var rawContent);
            symbolsByFile.TryGetValue(filePath, out var fileSymbols);
            evidencesByFile.TryGetValue(filePath, out var fileEvidences);

            var content = rawContent ?? string.Empty;
            var fileLines = content.Length > 0
                ? content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)
                : [];

            // Case A: Documentation file without extracted code symbols
            if (IsDocumentationFile(filePath) && (fileSymbols == null || fileSymbols.Count == 0))
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                GenerateDocumentationChunks(
                    filePath,
                    fileLines,
                    fileEvidences,
                    opts,
                    ref chunkIndex,
                    chunks);
                continue;
            }

            // Case B: Source file with extracted code symbols (classes, methods, endpoints, etc.)
            if (fileSymbols != null && fileSymbols.Count > 0)
            {
                // Deterministic symbol ordering: StartLine -> EndLine -> Name
                var orderedSymbols = fileSymbols
                    .OrderBy(s => s.Location!.Value.StartLine)
                    .ThenBy(s => s.Location!.Value.EndLine)
                    .ThenBy(s => s.Name, StringComparer.Ordinal)
                    .ToList();

                foreach (var symbol in orderedSymbols)
                {
                    var startLine = Math.Max(1, symbol.Location!.Value.StartLine);
                    var endLine = Math.Max(startLine, symbol.Location!.Value.EndLine);

                    string[] symbolLines;
                    if (fileLines.Length > 0 && startLine <= fileLines.Length)
                    {
                        var actualEnd = Math.Min(endLine, fileLines.Length);
                        var actualStart = Math.Max(1, startLine);
                        var count = actualEnd - actualStart + 1;
                        symbolLines = fileLines.Skip(actualStart - 1).Take(count).ToArray();
                    }
                    else
                    {
                        symbolLines = [$"Symbol: {symbol.Name} ({symbol.Type}) at {filePath}:{startLine}-{endLine}"];
                    }

                    // Split symbol lines into chunks if they exceed MaxChunkChars
                    var combinedSymbolContent = string.Join("\n", symbolLines);
                    List<(int SubStart, int SubEnd, string SubContent)> subChunks;

                    if (combinedSymbolContent.Length <= opts.MaxChunkChars)
                    {
                        subChunks = [(startLine, endLine, combinedSymbolContent)];
                    }
                    else
                    {
                        subChunks = SplitLinesIntoChunks(symbolLines, startLine, opts.MaxChunkChars, opts.OverlapLines);
                    }

                    foreach (var (subStart, subEnd, subContent) in subChunks)
                    {
                        var maskedContent = SecretMasker.MaskSecrets(subContent);
                        var tokenCount = Math.Max(1, maskedContent.Length / opts.ApproxCharsPerToken);
                        var primaryEvidenceKey = $"ev:{filePath}:{subStart}-{subEnd}";

                        var (overlappingIds, overlappingKeys, avgConf) =
                            FindOverlappingEvidences(fileEvidences, subStart, subEnd);

                        if (!overlappingKeys.Contains(primaryEvidenceKey, StringComparer.OrdinalIgnoreCase))
                        {
                            overlappingKeys.Insert(0, primaryEvidenceKey);
                        }

                        var confidence = avgConf ?? 0.85f; // High confidence for analyzed code symbols

                        chunks.Add(new GeneratedDocumentChunk(
                            FilePath: filePath,
                            Symbol: symbol.Name,
                            StartLine: subStart,
                            EndLine: subEnd,
                            Content: maskedContent,
                            TokenCount: tokenCount,
                            ChunkIndex: chunkIndex++,
                            EvidenceKey: primaryEvidenceKey,
                            EvidenceKeys: overlappingKeys.AsReadOnly(),
                            ConfidenceScore: confidence,
                            EvidenceIds: overlappingIds.Count > 0 ? overlappingIds.AsReadOnly() : null));
                    }
                }
                continue;
            }

            // Case C: File without code symbols (configs, scripts, manifests, etc.)
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var combinedFileContent = string.Join("\n", fileLines);
            List<(int SubStart, int SubEnd, string SubContent)> generalSubChunks;
            if (combinedFileContent.Length <= opts.MaxChunkChars)
            {
                generalSubChunks = [(1, Math.Max(1, fileLines.Length), combinedFileContent)];
            }
            else
            {
                generalSubChunks = SplitLinesIntoChunks(fileLines, 1, opts.MaxChunkChars, opts.OverlapLines);
            }

            foreach (var (subStart, subEnd, subContent) in generalSubChunks)
            {
                var maskedContent = SecretMasker.MaskSecrets(subContent);
                var tokenCount = Math.Max(1, maskedContent.Length / opts.ApproxCharsPerToken);
                var primaryEvidenceKey = $"ev:{filePath}:{subStart}-{subEnd}";

                var (overlappingIds, overlappingKeys, avgConf) =
                    FindOverlappingEvidences(fileEvidences, subStart, subEnd);

                if (!overlappingKeys.Contains(primaryEvidenceKey, StringComparer.OrdinalIgnoreCase))
                {
                    overlappingKeys.Insert(0, primaryEvidenceKey);
                }

                var confidence = avgConf ?? 1.0f; // Configuration files have exact confidence

                chunks.Add(new GeneratedDocumentChunk(
                    FilePath: filePath,
                    Symbol: null,
                    StartLine: subStart,
                    EndLine: subEnd,
                    Content: maskedContent,
                    TokenCount: tokenCount,
                    ChunkIndex: chunkIndex++,
                    EvidenceKey: primaryEvidenceKey,
                    EvidenceKeys: overlappingKeys.AsReadOnly(),
                    ConfidenceScore: confidence,
                    EvidenceIds: overlappingIds.Count > 0 ? overlappingIds.AsReadOnly() : null));
            }
        }

        return chunks.AsReadOnly();
    }

    /// <summary>
    /// Chunks documentation files (Markdown, text) along header / section boundaries.
    /// </summary>
    private static void GenerateDocumentationChunks(
        string filePath,
        string[] lines,
        IReadOnlyList<DomainEvidence>? fileEvidences,
        DocumentChunkOptions opts,
        ref int chunkIndex,
        List<GeneratedDocumentChunk> chunks)
    {
        // Parse markdown sections based on headings (# , ## , ### )
        var sections = ParseMarkdownSections(lines);

        foreach (var (heading, secStart, secEnd, secLines) in sections)
        {
            var combinedSecContent = string.Join("\n", secLines);
            List<(int SubStart, int SubEnd, string SubContent)> subChunks;
            if (combinedSecContent.Length <= opts.MaxChunkChars)
            {
                subChunks = [(secStart, secEnd, combinedSecContent)];
            }
            else
            {
                subChunks = SplitLinesIntoChunks(secLines, secStart, opts.MaxChunkChars, opts.OverlapLines);
            }

            foreach (var (subStart, subEnd, subContent) in subChunks)
            {
                var maskedContent = SecretMasker.MaskSecrets(subContent);
                var tokenCount = Math.Max(1, maskedContent.Length / opts.ApproxCharsPerToken);
                var primaryEvidenceKey = $"ev:{filePath}:{subStart}-{subEnd}";

                var (overlappingIds, overlappingKeys, avgConf) =
                    FindOverlappingEvidences(fileEvidences, subStart, subEnd);

                if (!overlappingKeys.Contains(primaryEvidenceKey, StringComparer.OrdinalIgnoreCase))
                {
                    overlappingKeys.Insert(0, primaryEvidenceKey);
                }

                var confidence = avgConf ?? 0.85f;

                chunks.Add(new GeneratedDocumentChunk(
                    FilePath: filePath,
                    Symbol: heading,
                    StartLine: subStart,
                    EndLine: subEnd,
                    Content: maskedContent,
                    TokenCount: tokenCount,
                    ChunkIndex: chunkIndex++,
                    EvidenceKey: primaryEvidenceKey,
                    EvidenceKeys: overlappingKeys.AsReadOnly(),
                    ConfidenceScore: confidence,
                    EvidenceIds: overlappingIds.Count > 0 ? overlappingIds.AsReadOnly() : null));
            }
        }
    }

    /// <summary>
    /// Identifies documentation files by extension or directory path.
    /// </summary>
    private static bool IsDocumentationFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".md" or ".markdown" or ".txt" or ".rst" or ".adoc")
        {
            return true;
        }

        var normalized = filePath.Replace('\\', '/');
        return normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/docs/", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("README", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses Markdown lines into logical sections based on markdown heading indicators (#).
    /// </summary>
    private static List<(string? Heading, int StartLine, int EndLine, string[] Lines)> ParseMarkdownSections(string[] lines)
    {
        var result = new List<(string? Heading, int StartLine, int EndLine, string[] Lines)>();
        if (lines.Length == 0) return result;

        var currentHeading = (string?)null;
        var sectionStart = 1;
        var sectionLines = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;

            if (IsMarkdownHeading(line, out var headingText))
            {
                // Emit prior section if it contains lines
                if (sectionLines.Count > 0)
                {
                    result.Add((currentHeading, sectionStart, lineNum - 1, sectionLines.ToArray()));
                    sectionLines.Clear();
                }

                currentHeading = headingText;
                sectionStart = lineNum;
            }

            sectionLines.Add(line);
        }

        if (sectionLines.Count > 0)
        {
            result.Add((currentHeading, sectionStart, sectionStart + sectionLines.Count - 1, sectionLines.ToArray()));
        }

        return result;
    }

    private static bool IsMarkdownHeading(string line, out string headingText)
    {
        headingText = string.Empty;
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('#')) return false;

        var hashCount = 0;
        while (hashCount < trimmed.Length && trimmed[hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount >= 1 && hashCount <= 6 && hashCount < trimmed.Length && char.IsWhiteSpace(trimmed[hashCount]))
        {
            headingText = trimmed.Trim();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Splits an array of lines into chunks without breaking lines, respecting maxChars and overlapLines.
    /// If a single line exceeds maxChars, it is sliced cleanly into segments.
    /// </summary>
    private static List<(int StartLine, int EndLine, string Content)> SplitLinesIntoChunks(
        string[] lines,
        int baseStartLine,
        int maxChars,
        int overlapLines)
    {
        var result = new List<(int StartLine, int EndLine, string Content)>();
        if (lines.Length == 0) return result;

        var currentLines = new List<string>();
        var currentChunkStart = baseStartLine;
        var currentChars = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var currentLineNumber = baseStartLine + i;

            // Handle very large single line
            if (line.Length > maxChars)
            {
                if (currentLines.Count > 0)
                {
                    var endLine = currentChunkStart + currentLines.Count - 1;
                    result.Add((currentChunkStart, endLine, string.Join("\n", currentLines)));
                    currentLines.Clear();
                    currentChars = 0;
                }

                for (var offset = 0; offset < line.Length; offset += maxChars)
                {
                    var len = Math.Min(maxChars, line.Length - offset);
                    var slice = line.Substring(offset, len);
                    result.Add((currentLineNumber, currentLineNumber, slice));
                }

                currentChunkStart = currentLineNumber + 1;
                continue;
            }

            var addition = (currentLines.Count > 0 ? 1 : 0) + line.Length;
            if (currentChars + addition > maxChars && currentLines.Count > 0)
            {
                var endLine = currentChunkStart + currentLines.Count - 1;
                result.Add((currentChunkStart, endLine, string.Join("\n", currentLines)));

                var overlap = Math.Min(overlapLines, currentLines.Count);
                if (overlap > 0)
                {
                    var overlapItems = currentLines.TakeLast(overlap).ToList();
                    currentLines.Clear();
                    currentLines.AddRange(overlapItems);
                    currentChunkStart = endLine - overlap + 1;
                    currentChars = currentLines.Sum(l => l.Length) + Math.Max(0, currentLines.Count - 1);
                }
                else
                {
                    currentLines.Clear();
                    currentChunkStart = currentLineNumber;
                    currentChars = 0;
                }
            }

            currentLines.Add(line);
            currentChars += (currentLines.Count > 1 ? 1 : 0) + line.Length;
        }

        if (currentLines.Count > 0)
        {
            var endLine = currentChunkStart + currentLines.Count - 1;
            result.Add((currentChunkStart, endLine, string.Join("\n", currentLines)));
        }

        return result;
    }

    /// <summary>
    /// Finds all existing Evidences whose file path matches and whose line range overlaps [startLine, endLine].
    /// </summary>
    private static (List<Guid> Ids, List<string> Keys, float? AvgConfidence) FindOverlappingEvidences(
        IReadOnlyList<DomainEvidence>? evidences,
        int startLine,
        int endLine)
    {
        var ids = new List<Guid>();
        var keys = new List<string>();
        var confidences = new List<float>();

        if (evidences != null)
        {
            foreach (var evi in evidences)
            {
                if (startLine <= evi.EndLine && endLine >= evi.StartLine)
                {
                    if (evi.Id != Guid.Empty && !ids.Contains(evi.Id))
                    {
                        ids.Add(evi.Id);
                    }

                    var key = $"ev:{evi.FilePath}:{evi.StartLine}-{evi.EndLine}";
                    if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    {
                        keys.Add(key);
                    }

                    if (evi.Confidence != null)
                    {
                        confidences.Add(evi.Confidence.Value);
                    }
                }
            }
        }

        float? avgConf = confidences.Count > 0 ? confidences.Average() : null;
        return (ids, keys, avgConf);
    }
}
