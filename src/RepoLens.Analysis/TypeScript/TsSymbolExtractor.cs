using System.Text;
using System.Text.RegularExpressions;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.TypeScript;

public enum TsSymbolKind
{
    Interface,
    Class,
    Type,
    Enum,
    Function,
    Constant
}

public sealed record TsDiscoveredSymbol(
    string Name,
    TsSymbolKind Kind,
    bool IsExported,
    SourceLocation Location,
    string Snippet);

/// <summary>
/// MVP regex/heuristic symbol extractor for TypeScript / JavaScript files without requiring a full Node.js or TypeScript compiler runtime.
/// </summary>
public partial class TsSymbolExtractor
{
    // Regex for: export? (default)? (interface|class|type|enum) Name
    [GeneratedRegex(@"^\s*(?<export>export\s+(?:default\s+)?)?(?<kind>interface|class|type|enum)\s+(?<name>[A-Za-z0-9_$]+)", RegexOptions.Multiline)]
    private static partial Regex TypeOrClassDeclarationRegex();

    // Regex for: export? (async)? function Name(...)
    [GeneratedRegex(@"^\s*(?<export>export\s+(?:default\s+)?)?(?:async\s+)?function\s+(?<name>[A-Za-z0-9_$]+)", RegexOptions.Multiline)]
    private static partial Regex FunctionDeclarationRegex();

    // Regex for: export? const Name = (async)? (...) => or function
    [GeneratedRegex(@"^\s*(?<export>export\s+)?const\s+(?<name>[A-Za-z0-9_$]+)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z0-9_$]+)\s*=>", RegexOptions.Multiline)]
    private static partial Regex ArrowFunctionOrConstRegex();

    /// <summary>
    /// Extracts symbols from TypeScript / JavaScript source code.
    /// </summary>
    public IReadOnlyList<TsDiscoveredSymbol> Extract(string sourceCode, string filePath = "unknown.ts")
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var symbols = new List<TsDiscoveredSymbol>();
        var lines = sourceCode.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        bool inBlockComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            int lineNumber = i + 1;
            var trimmed = line.Trim();

            if (inBlockComment)
            {
                if (trimmed.Contains("*/"))
                {
                    inBlockComment = false;
                }
                continue;
            }

            if (trimmed.StartsWith("/*"))
            {
                if (!trimmed.Contains("*/"))
                {
                    inBlockComment = true;
                }
                continue;
            }

            if (trimmed.StartsWith("//"))
            {
                continue;
            }

            // Check class / interface / type / enum
            var typeMatch = TypeOrClassDeclarationRegex().Match(line);
            if (typeMatch.Success)
            {
                var kindStr = typeMatch.Groups["kind"].Value;
                var name = typeMatch.Groups["name"].Value;
                var isExported = typeMatch.Groups["export"].Success;

                var kind = kindStr switch
                {
                    "interface" => TsSymbolKind.Interface,
                    "class" => TsSymbolKind.Class,
                    "type" => TsSymbolKind.Type,
                    "enum" => TsSymbolKind.Enum,
                    _ => TsSymbolKind.Type
                };

                symbols.Add(new TsDiscoveredSymbol(
                    Name: name,
                    Kind: kind,
                    IsExported: isExported,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: line.Trim()));

                continue;
            }

            // Check function declaration
            var funcMatch = FunctionDeclarationRegex().Match(line);
            if (funcMatch.Success)
            {
                var name = funcMatch.Groups["name"].Value;
                var isExported = funcMatch.Groups["export"].Success;

                symbols.Add(new TsDiscoveredSymbol(
                    Name: name,
                    Kind: TsSymbolKind.Function,
                    IsExported: isExported,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: line.Trim()));

                continue;
            }

            // Check arrow function / const export
            var arrowMatch = ArrowFunctionOrConstRegex().Match(line);
            if (arrowMatch.Success)
            {
                var name = arrowMatch.Groups["name"].Value;
                var isExported = arrowMatch.Groups["export"].Success;

                symbols.Add(new TsDiscoveredSymbol(
                    Name: name,
                    Kind: TsSymbolKind.Function,
                    IsExported: isExported,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: line.Trim()));
            }
        }

        return symbols;
    }

    /// <summary>
    /// Asynchronously extracts symbols from a TypeScript / JavaScript file on disk.
    /// </summary>
    public async Task<IReadOnlyList<TsDiscoveredSymbol>> ExtractFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found at path: {filePath}", filePath);
        }

        var sourceCode = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return Extract(sourceCode, filePath);
    }
}
