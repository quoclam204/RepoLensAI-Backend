using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

public sealed record CSharpInheritanceInfo(
    string DerivedType,
    string BaseType,
    bool IsInterfaceHeuristic,
    SourceLocation Location,
    string Snippet);

/// <summary>
/// Extracts base classes and implemented interfaces from C# type declarations.
/// </summary>
public class InheritanceExtractor : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly List<CSharpInheritanceInfo> _inheritances = [];

    public InheritanceExtractor(string filePath = "")
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath;
    }

    public IReadOnlyList<CSharpInheritanceInfo> Inheritances => _inheritances.AsReadOnly();

    public static IReadOnlyList<CSharpInheritanceInfo> ExtractFromTree(SyntaxTree tree, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(tree);

        var path = string.IsNullOrWhiteSpace(filePath) ? tree.FilePath : filePath;
        var extractor = new InheritanceExtractor(path);
        extractor.Visit(tree.GetRoot());
        return extractor.Inheritances;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        ExtractBaseTypes(node.Identifier.Text, node.BaseList, node);
        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        ExtractBaseTypes(node.Identifier.Text, node.BaseList, node);
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        ExtractBaseTypes(node.Identifier.Text, node.BaseList, node);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        ExtractBaseTypes(node.Identifier.Text, node.BaseList, node);
        base.VisitStructDeclaration(node);
    }

    private void ExtractBaseTypes(string typeName, BaseListSyntax? baseList, SyntaxNode node)
    {
        if (baseList is null || baseList.Types.Count == 0)
        {
            return;
        }

        var lineSpan = node.SyntaxTree.GetLineSpan(baseList.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        for (int i = 0; i < baseList.Types.Count; i++)
        {
            var baseType = baseList.Types[i].Type.ToString();
            var isInterface = LooksLikeInterface(baseType) || (node is InterfaceDeclarationSyntax);

            // In C#, if it's a class and has multiple base types, only the first one can be a base class (if not an interface)
            if (node is ClassDeclarationSyntax or RecordDeclarationSyntax && i > 0)
            {
                isInterface = true;
            }

            _inheritances.Add(new CSharpInheritanceInfo(
                DerivedType: typeName,
                BaseType: baseType,
                IsInterfaceHeuristic: isInterface,
                Location: location,
                Snippet: baseList.Types[i].ToString()));
        }
    }

    private static bool LooksLikeInterface(string typeName)
    {
        var simpleName = typeName.Split('.').Last();
        // C# convention: starts with 'I' followed by uppercase letter
        return simpleName.Length > 1 &&
               simpleName[0] == 'I' &&
               char.IsUpper(simpleName[1]);
    }
}
