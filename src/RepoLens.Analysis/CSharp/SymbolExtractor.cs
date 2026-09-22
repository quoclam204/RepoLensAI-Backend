using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

public enum CSharpSymbolKind
{
    Namespace,
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Method,
    Property,
    Constructor,
    Field
}

public sealed record CSharpDiscoveredSymbol(
    string Name,
    CSharpSymbolKind Kind,
    string? Namespace,
    string? ContainerType,
    SourceLocation Location,
    IReadOnlyList<string> Modifiers,
    string? ReturnType = null);

/// <summary>
/// Roslyn SyntaxWalker to extract classes, interfaces, records, structs, methods, and properties from C# code.
/// </summary>
public class SymbolExtractor : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly List<CSharpDiscoveredSymbol> _symbols = [];
    private readonly Stack<string> _containerStack = new();
    private string? _currentNamespace;

    public SymbolExtractor(string filePath = "")
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath;
    }

    public IReadOnlyList<CSharpDiscoveredSymbol> Symbols => _symbols.AsReadOnly();

    public static IReadOnlyList<CSharpDiscoveredSymbol> ExtractFromTree(SyntaxTree tree, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(tree);

        var path = string.IsNullOrWhiteSpace(filePath) ? tree.FilePath : filePath;
        var extractor = new SymbolExtractor(path);
        extractor.Visit(tree.GetRoot());
        return extractor.Symbols;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var prevNamespace = _currentNamespace;
        _currentNamespace = string.IsNullOrEmpty(_currentNamespace)
            ? node.Name.ToString()
            : $"{_currentNamespace}.{node.Name}";

        base.VisitNamespaceDeclaration(node);
        _currentNamespace = prevNamespace;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        AddTypeSymbol(node.Identifier.Text, CSharpSymbolKind.Class, node, node.Modifiers);

        _containerStack.Push(node.Identifier.Text);
        base.VisitClassDeclaration(node);
        _containerStack.Pop();
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        AddTypeSymbol(node.Identifier.Text, CSharpSymbolKind.Interface, node, node.Modifiers);

        _containerStack.Push(node.Identifier.Text);
        base.VisitInterfaceDeclaration(node);
        _containerStack.Pop();
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        AddTypeSymbol(node.Identifier.Text, CSharpSymbolKind.Struct, node, node.Modifiers);

        _containerStack.Push(node.Identifier.Text);
        base.VisitStructDeclaration(node);
        _containerStack.Pop();
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        AddTypeSymbol(node.Identifier.Text, CSharpSymbolKind.Record, node, node.Modifiers);

        _containerStack.Push(node.Identifier.Text);
        base.VisitRecordDeclaration(node);
        _containerStack.Pop();
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        AddTypeSymbol(node.Identifier.Text, CSharpSymbolKind.Enum, node, node.Modifiers);
        base.VisitEnumDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        var container = _containerStack.Count > 0 ? _containerStack.Peek() : null;
        var modifiers = node.Modifiers.Select(m => m.Text).ToList();
        var typeName = node.Declaration.Type.ToString();

        foreach (var variable in node.Declaration.Variables)
        {
            _symbols.Add(new CSharpDiscoveredSymbol(
                Name: variable.Identifier.Text,
                Kind: CSharpSymbolKind.Field,
                Namespace: _currentNamespace,
                ContainerType: container,
                Location: location,
                Modifiers: modifiers,
                ReturnType: typeName));
        }

        base.VisitFieldDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        var container = _containerStack.Count > 0 ? _containerStack.Peek() : null;
        var modifiers = node.Modifiers.Select(m => m.Text).ToList();

        _symbols.Add(new CSharpDiscoveredSymbol(
            Name: node.Identifier.Text,
            Kind: CSharpSymbolKind.Method,
            Namespace: _currentNamespace,
            ContainerType: container,
            Location: location,
            Modifiers: modifiers,
            ReturnType: node.ReturnType.ToString()));

        base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        var container = _containerStack.Count > 0 ? _containerStack.Peek() : null;
        var modifiers = node.Modifiers.Select(m => m.Text).ToList();

        _symbols.Add(new CSharpDiscoveredSymbol(
            Name: container is not null ? $"{container}..ctor" : ".ctor",
            Kind: CSharpSymbolKind.Constructor,
            Namespace: _currentNamespace,
            ContainerType: container,
            Location: location,
            Modifiers: modifiers,
            ReturnType: null));

        base.VisitConstructorDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        var container = _containerStack.Count > 0 ? _containerStack.Peek() : null;
        var modifiers = node.Modifiers.Select(m => m.Text).ToList();

        _symbols.Add(new CSharpDiscoveredSymbol(
            Name: node.Identifier.Text,
            Kind: CSharpSymbolKind.Property,
            Namespace: _currentNamespace,
            ContainerType: container,
            Location: location,
            Modifiers: modifiers,
            ReturnType: node.Type.ToString()));

        base.VisitPropertyDeclaration(node);
    }

    private void AddTypeSymbol(
        string name,
        CSharpSymbolKind kind,
        SyntaxNode node,
        SyntaxTokenList modifierTokens)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        var container = _containerStack.Count > 0 ? _containerStack.Peek() : null;
        var modifiers = modifierTokens.Select(m => m.Text).ToList();

        _symbols.Add(new CSharpDiscoveredSymbol(
            Name: name,
            Kind: kind,
            Namespace: _currentNamespace,
            ContainerType: container,
            Location: location,
            Modifiers: modifiers));
    }
}
