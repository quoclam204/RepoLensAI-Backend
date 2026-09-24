using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

/// <summary>
/// Represents a method call detected during static syntax analysis.
/// </summary>
/// <remarks>
/// LIMITATION: Call graph extraction via Roslyn CSharpSyntaxWalker operates purely at the syntax level without
/// semantic model compilation or type resolution. It cannot resolve overloads, dynamic dispatch, or
/// interface-to-implementation targets with certainty; hence calls carry ConfidenceScore.Medium.
/// </remarks>
public sealed record CSharpMethodCall(
    string CallerSymbol,
    string CalleeName,
    string FullInvocation,
    SourceLocation Location,
    string Snippet,
    ConfidenceScore Confidence);

/// <summary>
/// SyntaxWalker that extracts method invocations and establishes call relations between methods.
/// </summary>
/// <remarks>
/// Operates purely on syntax AST. Confidence is set to ConfidenceScore.Medium because method calls
/// cannot be resolved via semantic symbols without a full compilation context.
/// </remarks>
public class CallGraphExtractor : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly List<CSharpMethodCall> _calls = [];
    private string? _currentType;
    private string? _currentMember;

    public CallGraphExtractor(string filePath = "")
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath;
    }

    public IReadOnlyList<CSharpMethodCall> Calls => _calls.AsReadOnly();

    public static IReadOnlyList<CSharpMethodCall> ExtractFromTree(SyntaxTree tree, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(tree);

        var path = string.IsNullOrWhiteSpace(filePath) ? tree.FilePath : filePath;
        var extractor = new CallGraphExtractor(path);
        extractor.Visit(tree.GetRoot());
        return extractor.Calls;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var prevType = _currentType;
        _currentType = node.Identifier.Text;
        base.VisitClassDeclaration(node);
        _currentType = prevType;
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var prevType = _currentType;
        _currentType = node.Identifier.Text;
        base.VisitRecordDeclaration(node);
        _currentType = prevType;
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var prevType = _currentType;
        _currentType = node.Identifier.Text;
        base.VisitStructDeclaration(node);
        _currentType = prevType;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var prevMember = _currentMember;
        _currentMember = _currentType is not null
            ? $"{_currentType}.{node.Identifier.Text}"
            : node.Identifier.Text;

        base.VisitMethodDeclaration(node);
        _currentMember = prevMember;
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var prevMember = _currentMember;
        _currentMember = _currentType is not null
            ? $"{_currentType}..ctor"
            : ".ctor";

        base.VisitConstructorDeclaration(node);
        _currentMember = prevMember;
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var prevMember = _currentMember;
        _currentMember = _currentType is not null
            ? $"{_currentType}.{node.Identifier.Text}"
            : node.Identifier.Text;

        base.VisitPropertyDeclaration(node);
        _currentMember = prevMember;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var calleeName = ExtractCalleeName(node.Expression);

        if (!string.IsNullOrWhiteSpace(calleeName))
        {
            var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
            var location = new SourceLocation(
                _filePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.EndLinePosition.Line + 1);

            var caller = _currentMember ?? _currentType ?? "global";
            var snippet = node.ToString();

            _calls.Add(new CSharpMethodCall(
                CallerSymbol: caller,
                CalleeName: calleeName,
                FullInvocation: node.Expression.ToString(),
                Location: location,
                Snippet: snippet,
                Confidence: ConfidenceScore.Medium));
        }

        base.VisitInvocationExpression(node);
    }

    private static string ExtractCalleeName(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            GenericNameSyntax genericName => genericName.Identifier.Text,
            _ => expression.ToString().Split('.').Last().Split('(').First()
        };
    }
}
