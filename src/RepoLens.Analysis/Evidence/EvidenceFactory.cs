using Microsoft.CodeAnalysis;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.Evidence;

/// <summary>
/// Factory to create consistent, validated Domain Evidence instances from source code and AST nodes.
/// </summary>
public static class EvidenceFactory
{
    private const int MaxSnippetLength = 1000;

    /// <summary>
    /// Creates an Evidence record with explicit line ranges.
    /// </summary>
    public static Domain.Entities.Evidence Create(
        Guid analysisJobId,
        string filePath,
        int startLine,
        int endLine,
        string snippet,
        EvidenceType evidenceType,
        ConfidenceScore confidence,
        string? symbol = null)
    {
        var sanitizedSnippet = SanitizeSnippet(snippet);
        var location = new SourceLocation(filePath, Math.Max(1, startLine), Math.Max(startLine, endLine));

        return Domain.Entities.Evidence.Create(
            analysisJobId: analysisJobId,
            location: location,
            snippet: sanitizedSnippet,
            evidenceType: evidenceType,
            confidence: confidence,
            symbol: symbol);
    }

    /// <summary>
    /// Creates an Evidence record extracted from a Roslyn SyntaxNode.
    /// </summary>
    public static Domain.Entities.Evidence CreateFromSyntaxNode(
        Guid analysisJobId,
        string filePath,
        SyntaxNode node,
        EvidenceType evidenceType,
        ConfidenceScore confidence,
        string? symbol = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        int startLine = lineSpan.StartLinePosition.Line + 1; // 1-indexed
        int endLine = lineSpan.EndLinePosition.Line + 1;

        var rawSnippet = node.ToString();
        var sanitizedSnippet = SanitizeSnippet(rawSnippet);
        var location = new SourceLocation(filePath, startLine, endLine);

        return Domain.Entities.Evidence.Create(
            analysisJobId: analysisJobId,
            location: location,
            snippet: sanitizedSnippet,
            evidenceType: evidenceType,
            confidence: confidence,
            symbol: symbol);
    }

    /// <summary>
    /// Ensures snippet is non-empty, trimmed, and within safe length limits.
    /// </summary>
    public static string SanitizeSnippet(string? rawSnippet, int maxLength = MaxSnippetLength)
    {
        if (string.IsNullOrWhiteSpace(rawSnippet))
        {
            return "[No snippet available]";
        }

        var trimmed = rawSnippet.Trim();
        var masked = Security.SecretMasker.MaskSecrets(trimmed);
        if (masked.Length <= maxLength)
        {
            return masked;
        }

        return string.Concat(masked.AsSpan(0, maxLength - 3), "...");
    }
}
