using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

public sealed record DbSetInfo(
    string EntityTypeName,
    string PropertyName,
    string ContainingContextName,
    SourceLocation Location,
    string Snippet);

public sealed record DatabaseContextInfo(
    string ContextName,
    SourceLocation Location,
    string Snippet);

public sealed record DatabaseEntityInfo(
    string EntityName,
    string? TableName,
    IReadOnlyList<string> KeyProperties,
    SourceLocation Location,
    string Snippet);

/// <summary>
/// SyntaxWalker to detect EF Core DbContext declarations, DbSet&lt;T&gt; properties, and database entities.
/// </summary>
/// <remarks>
/// LIMITATION: This extractor currently detects entities via DbSet&lt;T&gt; properties on DbContext and Data Annotations
/// ([Table], [Key]). Fluent API configurations (e.g., OnModelCreating, modelBuilder.Entity&lt;T&gt;(),
/// IEntityTypeConfiguration&lt;T&gt;) are not currently analyzed in this static syntax pass.
/// </remarks>
public class DatabaseEntityExtractor : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly List<DatabaseContextInfo> _contexts = [];
    private readonly List<DbSetInfo> _dbSets = [];
    private readonly List<DatabaseEntityInfo> _entities = [];

    private string? _currentClassName;
    private bool _isCurrentClassDbContext;
    private readonly List<string> _currentClassKeys = [];

    public DatabaseEntityExtractor(string filePath = "")
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath;
    }

    public IReadOnlyList<DatabaseContextInfo> Contexts => _contexts.AsReadOnly();
    public IReadOnlyList<DbSetInfo> DbSets => _dbSets.AsReadOnly();
    public IReadOnlyList<DatabaseEntityInfo> Entities => _entities.AsReadOnly();

    public static (IReadOnlyList<DatabaseContextInfo> Contexts, IReadOnlyList<DbSetInfo> DbSets, IReadOnlyList<DatabaseEntityInfo> Entities) ExtractFromTree(
        SyntaxTree tree,
        string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(tree);

        var path = string.IsNullOrWhiteSpace(filePath) ? tree.FilePath : filePath;
        var extractor = new DatabaseEntityExtractor(path);
        extractor.Visit(tree.GetRoot());
        return (extractor.Contexts, extractor.DbSets, extractor.Entities);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var prevClass = _currentClassName;
        var prevIsDbContext = _isCurrentClassDbContext;
        _currentClassKeys.Clear();

        _currentClassName = node.Identifier.Text;
        _isCurrentClassDbContext = InheritsOrImplements(node, "DbContext");

        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        if (_isCurrentClassDbContext)
        {
            _contexts.Add(new DatabaseContextInfo(
                ContextName: _currentClassName,
                Location: location,
                Snippet: node.Identifier.Text));
        }

        var tableName = ExtractTableNameFromAttributes(node.AttributeLists);

        base.VisitClassDeclaration(node);

        if (!_isCurrentClassDbContext && (tableName is not null || _currentClassKeys.Count > 0))
        {
            _entities.Add(new DatabaseEntityInfo(
                EntityName: _currentClassName,
                TableName: tableName,
                KeyProperties: [.. _currentClassKeys],
                Location: location,
                Snippet: node.Identifier.Text));
        }

        _currentClassName = prevClass;
        _isCurrentClassDbContext = prevIsDbContext;
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var location = new SourceLocation(
            _filePath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);

        // Check if this property is a DbSet<T>
        if (_isCurrentClassDbContext && _currentClassName is not null && node.Type is GenericNameSyntax genericType)
        {
            if (genericType.Identifier.Text == "DbSet" && genericType.TypeArgumentList.Arguments.Count == 1)
            {
                var entityTypeName = genericType.TypeArgumentList.Arguments[0].ToString();
                var propertyName = node.Identifier.Text;

                _dbSets.Add(new DbSetInfo(
                    EntityTypeName: entityTypeName,
                    PropertyName: propertyName,
                    ContainingContextName: _currentClassName,
                    Location: location,
                    Snippet: node.ToString()));
            }
        }

        // Check if property is marked with [Key]
        if (HasKeyAttribute(node.AttributeLists))
        {
            _currentClassKeys.Add(node.Identifier.Text);
        }

        base.VisitPropertyDeclaration(node);
    }

    private static bool InheritsOrImplements(ClassDeclarationSyntax node, string expectedBase)
    {
        if (node.BaseList is null) return false;

        return node.BaseList.Types.Any(t =>
        {
            var typeName = t.Type.ToString();
            return typeName.Equals(expectedBase, StringComparison.OrdinalIgnoreCase) ||
                   typeName.EndsWith($".{expectedBase}", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string? ExtractTableNameFromAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name.Equals("Table", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TableAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    if (attribute.ArgumentList?.Arguments.Count > 0)
                    {
                        var expr = attribute.ArgumentList.Arguments[0].Expression;
                        if (expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                        {
                            return literal.Token.ValueText;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool HasKeyAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("KeyAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
