using System.Text;
using System.Text.RegularExpressions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Services;

/// <summary>
/// Service implementing deterministic AI evidence validation (T088 / NFR-AI-001).
/// Verifies the grounding relationship between an AI-generated answer and retrieved repository evidence chunks:
/// 1. Rejects hallucinated AI citations whose files or symbols do not match retrieved chunks.
/// 2. Detects in-text claims asserting repository files or code symbols not present in retrieved evidence.
/// 3. Marks unsupported claims as uncertain, removes them, or returns an insufficient-evidence response.
/// 4. Ensures only verified, grounded evidence items are passed downstream.
/// </summary>
public partial class AiEvidenceValidator : IAiEvidenceValidator
{
    private readonly AnswerValidationOptions _options;

    // Pattern to identify file path references in text (e.g. `src/Auth/TokenService.cs` or `TokenService.cs`)
    [GeneratedRegex(@"`?([a-zA-Z0-9_\-\./\\]+\.(?:cs|ts|js|json|xml|csproj|sln|sql|md|ya?ml))`?", RegexOptions.IgnoreCase)]
    private static partial Regex FilePathRegex();

    // Pattern to identify backticked code identifiers (e.g. `ProcessOrder`, `IUserRepository`)
    [GeneratedRegex(@"`([a-zA-Z_][a-zA-Z0-9_]{2,})`")]
    private static partial Regex BacktickedSymbolRegex();

    // Standard non-domain code keywords to exclude from symbol extraction
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "string", "int", "bool", "void", "async", "await", "task",
        "var", "let", "const", "class", "interface", "record", "struct", "enum", "public",
        "private", "protected", "internal", "static", "readonly", "override", "virtual",
        "get", "set", "init", "return", "new", "this", "base", "if", "else", "foreach",
        "while", "try", "catch", "finally", "throw", "using", "namespace", "import", "export",
        "from", "default", "select", "where", "from", "post", "put", "delete", "patch", "api"
    };

    public AiEvidenceValidator(AnswerValidationOptions? options = null)
    {
        _options = options ?? AnswerValidationOptions.Default;
    }

    /// <inheritdoc />
    public Task<AnswerValidationResult> ValidateAnswerAsync(
        string answer,
        IReadOnlyList<VectorChunkSearchResult> retrievedChunks,
        IReadOnlyList<AiEvidenceItem>? aiCitations = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AnswerValidationRequest(answer, retrievedChunks, aiCitations);
        return ValidateAnswerAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AnswerValidationResult> ValidateAnswerAsync(
        AnswerValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var chunks = request.RetrievedChunks ?? [];
        var answer = request.Answer ?? string.Empty;
        var citations = request.AiCitations ?? [];

        // 1. Handle empty / whitespace answer
        if (string.IsNullOrWhiteSpace(answer))
        {
            return Task.FromResult(new AnswerValidationResult(
                IsValid: false,
                Status: AnswerValidationStatus.InsufficientEvidence,
                ValidatedAnswer: _options.InsufficientEvidenceMessage,
                ValidatedEvidence: [],
                ClaimDetails: [],
                UnsupportedClaims: ["Empty answer provided."],
                RejectedCitations: []));
        }

        // 2. Handle zero retrieved evidence chunks
        if (chunks.Count == 0)
        {
            var isAlreadyAcknowledgingNoEvidence =
                answer.Contains("insufficient evidence", StringComparison.OrdinalIgnoreCase) ||
                answer.Contains("no relevant evidence", StringComparison.OrdinalIgnoreCase) ||
                answer.Contains("no evidence found", StringComparison.OrdinalIgnoreCase) ||
                answer.Contains("cannot find any evidence", StringComparison.OrdinalIgnoreCase);

            if (isAlreadyAcknowledgingNoEvidence)
            {
                return Task.FromResult(new AnswerValidationResult(
                    IsValid: true,
                    Status: AnswerValidationStatus.InsufficientEvidence,
                    ValidatedAnswer: answer,
                    ValidatedEvidence: [],
                    ClaimDetails: [new ClaimValidationItem(answer, true, null, null, "Answer correctly states lack of evidence.")],
                    UnsupportedClaims: [],
                    RejectedCitations: citations));
            }

            // An answer asserting specific repository facts with 0 chunks is completely unevidenced
            return Task.FromResult(new AnswerValidationResult(
                IsValid: false,
                Status: AnswerValidationStatus.InsufficientEvidence,
                ValidatedAnswer: _options.InsufficientEvidenceMessage,
                ValidatedEvidence: [],
                ClaimDetails: [new ClaimValidationItem(answer, false, null, null, "No evidence chunks were retrieved to support this answer.")],
                UnsupportedClaims: [answer],
                RejectedCitations: citations));
        }

        // 3. Validate AI-provided citations against retrieved chunks
        var verifiedCitations = new List<AiEvidenceItem>();
        var rejectedCitations = new List<AiEvidenceItem>();

        foreach (var citation in citations)
        {
            if (IsCitationSupportedByChunks(citation, chunks))
            {
                verifiedCitations.Add(citation);
            }
            else
            {
                rejectedCitations.Add(citation);
            }
        }

        // 4. Build verified base evidence list from retrieved chunks
        var validatedEvidence = new List<AiEvidenceItem>(chunks.Count + verifiedCitations.Count);
        foreach (var chunk in chunks)
        {
            validatedEvidence.Add(new AiEvidenceItem
            {
                File = chunk.FilePath,
                Symbol = chunk.Symbol,
                StartLine = chunk.StartLine,
                EndLine = chunk.EndLine,
                Reason = $"Grounded from chunk {chunk.ChunkIndex} (similarity: {chunk.SimilarityScore:P0}, confidence: {chunk.ConfidenceScore:F2})"
            });
        }

        // Merge any verified citations from AI that aren't exact duplicates
        foreach (var verified in verifiedCitations)
        {
            if (!validatedEvidence.Any(e =>
                MatchesFilePath(e.File, verified.File) &&
                e.StartLine == verified.StartLine &&
                e.EndLine == verified.EndLine))
            {
                validatedEvidence.Add(verified);
            }
        }

        // 5. Extract and validate claims in the answer text
        var sentences = SplitIntoSentences(answer);
        var claimDetails = new List<ClaimValidationItem>(sentences.Count);
        var unsupportedClaims = new List<string>();

        foreach (var sentence in sentences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sentence))
            {
                continue;
            }

            var (isSupported, matchedFile, matchedSymbol, reason) = EvaluateSentenceGrounding(sentence, chunks);

            claimDetails.Add(new ClaimValidationItem(
                ClaimText: sentence,
                IsSupported: isSupported,
                MatchedFile: matchedFile,
                MatchedSymbol: matchedSymbol,
                Reason: reason));

            if (!isSupported)
            {
                unsupportedClaims.Add(sentence);
            }
        }

        // 6. Build final validated answer according to policy
        var hasUnsupportedClaims = unsupportedClaims.Count > 0;
        var hasRejectedCitations = rejectedCitations.Count > 0;

        AnswerValidationStatus status;
        bool isValid;
        string validatedAnswer;

        if (!hasUnsupportedClaims && !hasRejectedCitations)
        {
            status = AnswerValidationStatus.FullySupported;
            isValid = true;
            validatedAnswer = answer;
        }
        else if (unsupportedClaims.Count == sentences.Count)
        {
            status = AnswerValidationStatus.Unsupported;
            isValid = false;
            validatedAnswer = _options.InsufficientEvidenceMessage;
        }
        else
        {
            status = AnswerValidationStatus.PartiallySupported;
            isValid = false;

            validatedAnswer = _options.ActionOnUnsupportedClaims switch
            {
                UnsupportedClaimAction.Remove => BuildAnswerWithRemovedClaims(sentences, unsupportedClaims),
                UnsupportedClaimAction.RejectIfAny => _options.InsufficientEvidenceMessage,
                _ => BuildAnswerWithUncertaintyTags(sentences, unsupportedClaims)
            };
        }

        return Task.FromResult(new AnswerValidationResult(
            IsValid: isValid,
            Status: status,
            ValidatedAnswer: validatedAnswer,
            ValidatedEvidence: validatedEvidence.AsReadOnly(),
            ClaimDetails: claimDetails.AsReadOnly(),
            UnsupportedClaims: unsupportedClaims.AsReadOnly(),
            RejectedCitations: rejectedCitations.AsReadOnly()));
    }

    private static bool IsCitationSupportedByChunks(AiEvidenceItem citation, IReadOnlyList<VectorChunkSearchResult> chunks)
    {
        if (string.IsNullOrWhiteSpace(citation.File))
        {
            return false;
        }

        foreach (var chunk in chunks)
        {
            if (!MatchesFilePath(chunk.FilePath, citation.File))
            {
                continue;
            }

            // If a symbol is asserted in the citation, check if chunk supports it
            if (!string.IsNullOrWhiteSpace(citation.Symbol))
            {
                var symbolMatches = (chunk.Symbol != null && chunk.Symbol.Contains(citation.Symbol, StringComparison.OrdinalIgnoreCase)) ||
                                    chunk.Content.Contains(citation.Symbol, StringComparison.Ordinal);
                if (!symbolMatches)
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }

    private static (bool IsSupported, string? MatchedFile, string? MatchedSymbol, string Reason) EvaluateSentenceGrounding(
        string sentence,
        IReadOnlyList<VectorChunkSearchResult> chunks)
    {
        // 1. Check for referenced file paths in the sentence
        var fileMatches = FilePathRegex().Matches(sentence);
        foreach (Match match in fileMatches)
        {
            var referencedFile = match.Groups[1].Value.Trim('`', ' ', '\'', '"');
            var matchingChunk = chunks.FirstOrDefault(c => MatchesFilePath(c.FilePath, referencedFile));

            if (matchingChunk == null)
            {
                return (false, referencedFile, null, $"Referenced file '{referencedFile}' is not present in retrieved repository evidence.");
            }
        }

        // 2. Check for backticked code symbols in the sentence
        var symbolMatches = BacktickedSymbolRegex().Matches(sentence);
        foreach (Match match in symbolMatches)
        {
            var symbol = match.Groups[1].Value;
            if (ReservedKeywords.Contains(symbol))
            {
                continue;
            }

            var symbolFoundInEvidence = chunks.Any(c =>
                (c.Symbol != null && c.Symbol.Contains(symbol, StringComparison.OrdinalIgnoreCase)) ||
                c.Content.Contains(symbol, StringComparison.Ordinal));

            if (!symbolFoundInEvidence)
            {
                return (false, null, symbol, $"Referenced code symbol '{symbol}' is not present in retrieved repository evidence.");
            }
        }

        // If files or symbols were found and verified, report them
        string? matchedFile = null;
        if (fileMatches.Count > 0)
        {
            matchedFile = fileMatches[0].Groups[1].Value.Trim('`', ' ', '\'', '"');
        }

        string? matchedSymbol = null;
        if (symbolMatches.Count > 0)
        {
            matchedSymbol = symbolMatches[0].Groups[1].Value;
        }

        return (true, matchedFile, matchedSymbol, "Claim is consistent with retrieved evidence.");
    }

    private static bool MatchesFilePath(string chunkPath, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(chunkPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        var normalizedChunk = NormalizePath(chunkPath);
        var normalizedTarget = NormalizePath(targetPath);

        if (normalizedChunk.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedChunk.EndsWith("/" + normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedTarget.EndsWith("/" + normalizedChunk, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var chunkFileName = Path.GetFileName(normalizedChunk);
        var targetFileName = Path.GetFileName(normalizedTarget);

        return !string.IsNullOrEmpty(chunkFileName) &&
               chunkFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var result = new List<string>();
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            // Split line into sentences on . ! ? followed by whitespace or end of line
            var rawSentences = Regex.Split(trimmedLine, @"(?<=[.!?])\s+(?=[A-Z0-9`""'])");
            foreach (var s in rawSentences)
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }

        return result;
    }

    private string BuildAnswerWithUncertaintyTags(List<string> sentences, List<string> unsupportedClaims)
    {
        var sb = new StringBuilder();
        var unsupportedSet = new HashSet<string>(unsupportedClaims);

        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            if (unsupportedSet.Contains(sentence))
            {
                sb.Append($"{_options.UncertaintyPrefix}: {sentence}");
            }
            else
            {
                sb.Append(sentence);
            }

            if (i < sentences.Count - 1)
            {
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }

    private static string BuildAnswerWithRemovedClaims(List<string> sentences, List<string> unsupportedClaims)
    {
        var sb = new StringBuilder();
        var unsupportedSet = new HashSet<string>(unsupportedClaims);

        var first = true;
        foreach (var sentence in sentences)
        {
            if (unsupportedSet.Contains(sentence))
            {
                continue;
            }

            if (!first)
            {
                sb.Append(' ');
            }

            sb.Append(sentence);
            first = false;
        }

        return sb.ToString();
    }
}
