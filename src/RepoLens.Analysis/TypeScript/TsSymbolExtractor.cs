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
    Constant,
    Component
}

public sealed record TsDiscoveredSymbol(
    string Name,
    TsSymbolKind Kind,
    bool IsExported,
    SourceLocation Location,
    string Snippet);

public sealed record TsDiscoveredImport(
    string ModulePath,
    SourceLocation Location,
    string Snippet);

public sealed record TsDiscoveredRoute(
    string RouteTemplate,
    string HttpMethod,
    string? HandlerOrComponent,
    SourceLocation Location,
    string Snippet);

public sealed record TsDiscoveredApiCall(
    string HttpMethod,
    string EndpointUrl,
    SourceLocation Location,
    string Snippet);

/// <summary>
/// MVP regex/heuristic symbol, route, and API call extractor for TypeScript / JavaScript files
/// without requiring a full Node.js or TypeScript compiler runtime (T047, T048, T049, T051).
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

    // Regex for: import ... from '...'
    [GeneratedRegex(@"^\s*import\s+(?:(?:(?:\*\s+as\s+[A-Za-z0-9_$]+|\{[^}]*\}|[A-Za-z0-9_$]+)\s*,?\s*)*(?:\{[^}]*\}\s*)?)?from\s+['""](?<module>[^'""]+)['""]", RegexOptions.Multiline)]
    private static partial Regex ImportDeclarationRegex();

    // Regex for fetch('url') or fetch("url") or fetch(`url`)
    [GeneratedRegex(@"(?i)\bfetch\s*\(\s*['""`](?<url>[^'""`]+)['""`]")]
    private static partial Regex FetchCallRegex();

    // Regex for axios.get('url'), apiClient.post('url'), http.get('url'), etc.
    [GeneratedRegex(@"(?i)\b(?:axios|apiClient|api|http|client)\.(?<method>get|post|put|delete|patch)\s*\(\s*['""`](?<url>[^'""`]+)['""`]")]
    private static partial Regex AxiosOrClientCallRegex();

    // Regex for React Route: <Route path="/users" element={<Users />} /> or component={Users}
    [GeneratedRegex(@"(?i)<Route\s+[^>]*path=['""](?<route>[^'""]+)['""]")]
    private static partial Regex ReactRoutePathRegex();

    [GeneratedRegex(@"(?i)(?:element=\{<|component=\{)(?<comp>[A-Za-z0-9_$]+)")]
    private static partial Regex ReactComponentRefRegex();

    // Regex for express route: app.get('/api/users', ...) or router.post('/api/users', ...)
    [GeneratedRegex(@"(?i)\b(?:app|router)\.(?<method>get|post|put|delete|patch)\s*\(\s*['""](?<route>[^'""]+)['""]")]
    private static partial Regex ExpressRouteRegex();

    /// <summary>
    /// Extracts symbols from TypeScript / JavaScript source code.
    /// </summary>
    public IReadOnlyList<TsDiscoveredSymbol> Extract(string sourceCode, string filePath = "unknown.ts")
    {
        return ExtractAll(sourceCode, filePath).Symbols;
    }

    /// <summary>
    /// Extracts both declared symbols and module imports from TypeScript / JavaScript source code.
    /// </summary>
    public (IReadOnlyList<TsDiscoveredSymbol> Symbols, IReadOnlyList<TsDiscoveredImport> Imports) ExtractWithImports(string sourceCode, string filePath = "unknown.ts")
    {
        var all = ExtractAll(sourceCode, filePath);
        return (all.Symbols, all.Imports);
    }

    /// <summary>
    /// Extracts declared symbols, module imports, frontend routes, and API calls from TypeScript / JavaScript source code.
    /// </summary>
    public (IReadOnlyList<TsDiscoveredSymbol> Symbols,
            IReadOnlyList<TsDiscoveredImport> Imports,
            IReadOnlyList<TsDiscoveredRoute> Routes,
            IReadOnlyList<TsDiscoveredApiCall> ApiCalls) ExtractAll(string sourceCode, string filePath = "unknown.ts")
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var symbols = new List<TsDiscoveredSymbol>();
        var imports = new List<TsDiscoveredImport>();
        var routes = new List<TsDiscoveredRoute>();
        var apiCalls = new List<TsDiscoveredApiCall>();

        var lines = sourceCode.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        bool inBlockComment = false;
        bool isJsxFile = filePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                         filePath.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase);

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

            // 1. Check import declaration: import ... from '...'
            var importMatch = ImportDeclarationRegex().Match(line);
            if (importMatch.Success)
            {
                var modulePath = importMatch.Groups["module"].Value;
                imports.Add(new TsDiscoveredImport(
                    ModulePath: modulePath,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: trimmed));
                continue;
            }

            // 2. Check React Route: <Route path="/users" ... />
            var reactRouteMatch = ReactRoutePathRegex().Match(line);
            if (reactRouteMatch.Success)
            {
                var routeTemplate = reactRouteMatch.Groups["route"].Value;
                var compMatch = ReactComponentRefRegex().Match(line);
                var comp = compMatch.Success ? compMatch.Groups["comp"].Value : null;

                routes.Add(new TsDiscoveredRoute(
                    RouteTemplate: routeTemplate,
                    HttpMethod: "GET",
                    HandlerOrComponent: comp,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: trimmed));
            }

            // 3. Check Express / router route: app.get('/api/...', ...)
            var expressRouteMatch = ExpressRouteRegex().Match(line);
            if (expressRouteMatch.Success)
            {
                var method = expressRouteMatch.Groups["method"].Value.ToUpperInvariant();
                var routeTemplate = expressRouteMatch.Groups["route"].Value;

                routes.Add(new TsDiscoveredRoute(
                    RouteTemplate: routeTemplate,
                    HttpMethod: method,
                    HandlerOrComponent: $"{Path.GetFileNameWithoutExtension(filePath)}.{method}",
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: trimmed));
            }

            // 4. Check API call: axios.get(...), apiClient.post(...), http.get(...)
            var clientCallMatch = AxiosOrClientCallRegex().Match(line);
            if (clientCallMatch.Success)
            {
                var method = clientCallMatch.Groups["method"].Value.ToUpperInvariant();
                var url = clientCallMatch.Groups["url"].Value;

                apiCalls.Add(new TsDiscoveredApiCall(
                    HttpMethod: method,
                    EndpointUrl: url,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: trimmed));
            }
            else
            {
                // Check fetch('...')
                var fetchMatch = FetchCallRegex().Match(line);
                if (fetchMatch.Success)
                {
                    var url = fetchMatch.Groups["url"].Value;
                    apiCalls.Add(new TsDiscoveredApiCall(
                        HttpMethod: "GET",
                        EndpointUrl: url,
                        Location: new SourceLocation(filePath, lineNumber, lineNumber),
                        Snippet: trimmed));
                }
            }

            // 5. Check class / interface / type / enum
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

            // 6. Check function declaration
            var funcMatch = FunctionDeclarationRegex().Match(line);
            if (funcMatch.Success)
            {
                var name = funcMatch.Groups["name"].Value;
                var isExported = funcMatch.Groups["export"].Success;
                var isComponent = isJsxFile && char.IsUpper(name[0]);

                symbols.Add(new TsDiscoveredSymbol(
                    Name: name,
                    Kind: isComponent ? TsSymbolKind.Component : TsSymbolKind.Function,
                    IsExported: isExported,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: line.Trim()));

                continue;
            }

            // 7. Check arrow function / const export
            var arrowMatch = ArrowFunctionOrConstRegex().Match(line);
            if (arrowMatch.Success)
            {
                var name = arrowMatch.Groups["name"].Value;
                var isExported = arrowMatch.Groups["export"].Success;
                var isComponent = isJsxFile && char.IsUpper(name[0]);

                symbols.Add(new TsDiscoveredSymbol(
                    Name: name,
                    Kind: isComponent ? TsSymbolKind.Component : TsSymbolKind.Function,
                    IsExported: isExported,
                    Location: new SourceLocation(filePath, lineNumber, lineNumber),
                    Snippet: line.Trim()));
            }
        }

        return (symbols.AsReadOnly(), imports.AsReadOnly(), routes.AsReadOnly(), apiCalls.AsReadOnly());
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
