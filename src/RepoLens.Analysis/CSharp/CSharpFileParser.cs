using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepoLens.Analysis.CSharp;

/// <summary>
/// Parser to read C# source code or files into Roslyn SyntaxTree instances.
/// </summary>
public class CSharpFileParser
{
    private readonly CSharpParseOptions _parseOptions;

    public CSharpFileParser(LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        _parseOptions = new CSharpParseOptions(
            languageVersion: languageVersion,
            documentationMode: DocumentationMode.Parse);
    }

    /// <summary>
    /// Parses raw C# code string into a SyntaxTree.
    /// </summary>
    public SyntaxTree ParseText(string text, string filePath = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        return CSharpSyntaxTree.ParseText(
            text: text,
            options: _parseOptions,
            path: filePath,
            encoding: Encoding.UTF8,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads and parses a C# source file from disk.
    /// </summary>
    public async Task<SyntaxTree> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"C# file not found at path: {filePath}", filePath);
        }

        var sourceText = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseText(sourceText, filePath, cancellationToken);
    }

    /// <summary>
    /// Extracts the CompilationUnitSyntax root from a SyntaxTree.
    /// </summary>
    public CompilationUnitSyntax GetCompilationUnit(SyntaxTree syntaxTree, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        var root = syntaxTree.GetRoot(cancellationToken);
        if (root is CompilationUnitSyntax compilationUnit)
        {
            return compilationUnit;
        }

        throw new InvalidOperationException($"Syntax root is not a {nameof(CompilationUnitSyntax)}.");
    }
}
