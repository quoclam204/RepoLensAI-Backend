using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

public sealed record ApiEndpointInfo(
    string HttpMethod,
    string RouteTemplate,
    string HandlerSymbol,
    bool IsMinimalApi,
    SourceLocation Location,
    string Snippet);

/// <summary>
/// SyntaxWalker to detect ASP.NET Core controller endpoints and Minimal API endpoint definitions.
/// </summary>
public class ApiEndpointExtractor : CSharpSyntaxWalker
{
    private static readonly HashSet<string> HttpAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions",
        "HttpGetAttribute", "HttpPostAttribute", "HttpPutAttribute", "HttpDeleteAttribute",
        "HttpPatchAttribute", "HttpHeadAttribute", "HttpOptionsAttribute"
    };

    private static readonly HashSet<string> MinimalApiMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch"
    };

    private readonly string _filePath;
    private readonly List<ApiEndpointInfo> _endpoints = [];
    private string? _currentControllerName;
    private string? _controllerRoutePrefix;

    public ApiEndpointExtractor(string filePath = "")
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath;
    }

    public IReadOnlyList<ApiEndpointInfo> Endpoints => _endpoints.AsReadOnly();

    public static IReadOnlyList<ApiEndpointInfo> ExtractFromTree(SyntaxTree tree, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(tree);

        var path = string.IsNullOrWhiteSpace(filePath) ? tree.FilePath : filePath;
        var extractor = new ApiEndpointExtractor(path);
        extractor.Visit(tree.GetRoot());
        return extractor.Endpoints;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var prevController = _currentControllerName;
        var prevPrefix = _controllerRoutePrefix;

        _currentControllerName = node.Identifier.Text;
        _controllerRoutePrefix = ExtractRouteFromAttributes(node.AttributeLists);

        base.VisitClassDeclaration(node);

        _currentControllerName = prevController;
        _controllerRoutePrefix = prevPrefix;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (_currentControllerName is not null)
        {
            foreach (var attributeList in node.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attrName = attribute.Name.ToString();
                    var matchedMethod = GetHttpMethodFromAttribute(attrName);

                    if (matchedMethod is not null)
                    {
                        var actionRoute = ExtractRouteFromAttributeArgument(attribute);
                        var fullRoute = CombineRoutes(_controllerRoutePrefix, actionRoute, _currentControllerName);

                        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
                        var location = new SourceLocation(
                            _filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.EndLinePosition.Line + 1);

                        _endpoints.Add(new ApiEndpointInfo(
                            HttpMethod: matchedMethod,
                            RouteTemplate: fullRoute,
                            HandlerSymbol: $"{_currentControllerName}.{node.Identifier.Text}",
                            IsMinimalApi: false,
                            Location: location,
                            Snippet: node.Identifier.Text));
                    }
                }
            }
        }

        base.VisitMethodDeclaration(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var methodName = node.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        if (methodName is not null && MinimalApiMethods.Contains(methodName))
        {
            var httpMethod = methodName.Replace("Map", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
            var route = ExtractFirstStringArgument(node.ArgumentList) ?? "/";

            var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
            var location = new SourceLocation(
                _filePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.EndLinePosition.Line + 1);

            _endpoints.Add(new ApiEndpointInfo(
                HttpMethod: httpMethod,
                RouteTemplate: route,
                HandlerSymbol: $"MinimalApi:{methodName}",
                IsMinimalApi: true,
                Location: location,
                Snippet: node.ToString()));
        }

        base.VisitInvocationExpression(node);
    }

    private static string? GetHttpMethodFromAttribute(string attributeName)
    {
        var normalized = attributeName.Split('.').Last();
        if (normalized.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^9];
        }

        return normalized.ToUpperInvariant() switch
        {
            "HTTPGET" => "GET",
            "HTTPPOST" => "POST",
            "HTTPPUT" => "PUT",
            "HTTPDELETE" => "DELETE",
            "HTTPPATCH" => "PATCH",
            "HTTPHEAD" => "HEAD",
            "HTTPOPTIONS" => "OPTIONS",
            _ => null
        };
    }

    private static string? ExtractRouteFromAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name.Contains("Route", StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractRouteFromAttributeArgument(attribute);
                }
            }
        }

        return null;
    }

    private static string? ExtractRouteFromAttributeArgument(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArg = attribute.ArgumentList.Arguments[0].Expression;
        if (firstArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private static string? ExtractFirstStringArgument(ArgumentListSyntax? argumentList)
    {
        if (argumentList is null || argumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArg = argumentList.Arguments[0].Expression;
        if (firstArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private static string CombineRoutes(string? prefix, string? actionRoute, string? controllerName)
    {
        var routePrefix = prefix ?? "";
        if (controllerName is not null && routePrefix.Contains("[controller]", StringComparison.OrdinalIgnoreCase))
        {
            var shortController = controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                ? controllerName[..^10]
                : controllerName;
            routePrefix = routePrefix.Replace("[controller]", shortController, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrEmpty(routePrefix))
        {
            return actionRoute ?? "/";
        }

        if (string.IsNullOrEmpty(actionRoute))
        {
            return routePrefix.StartsWith('/') ? routePrefix : $"/{routePrefix}";
        }

        var combined = $"{routePrefix.TrimEnd('/')}/{actionRoute.TrimStart('/')}";
        return combined.StartsWith('/') ? combined : $"/{combined}";
    }
}
