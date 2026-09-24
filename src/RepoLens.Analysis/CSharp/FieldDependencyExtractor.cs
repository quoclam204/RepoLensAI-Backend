using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

public enum FieldDependencyKind
{
    Field,
    ConstructorParameter
}

public sealed record FieldDependency(
    string SourceClass,
    string TargetType,
    FieldDependencyKind Kind,
    SourceLocation Location,
    string Snippet,
    ConfidenceScore Confidence);

/// <summary>
/// SyntaxWalker that detects dependency injection patterns via class fields and constructor parameters.
/// </summary>
/// <remarks>
/// LIMITATION: Field and constructor dependency extraction operates purely at the syntax AST level without
/// semantic type binding. It cannot verify whether a referenced type is truly an internal project class or an
/// external library type. Furthermore, filtering out enums by naming suffixes (Status, Type, Stage, Kind, Mode)
/// is a heuristic and may occasionally filter or include unintended types; hence dependencies are assigned ConfidenceScore.Medium.
/// </remarks>
public class FieldDependencyExtractor : CSharpSyntaxWalker
{
    private static readonly HashSet<string> ExcludedPrimitivesAndKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort",
        "float", "double", "decimal", "bool", "char", "string", "object", "void", "dynamic", "var",
        "Int16", "Int32", "Int64", "UInt16", "UInt32", "UInt64", "Single", "Double", "Decimal",
        "Boolean", "Char", "String", "Object", "Byte", "SByte"
    };

    private static readonly HashSet<string> ExcludedFrameworkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Guid", "DateTime", "DateTimeOffset", "TimeSpan", "DateOnly", "TimeOnly",
        "CancellationToken", "Task", "ValueTask", "Action", "Func", "Predicate", "EventHandler",
        "Exception", "Uri", "StringBuilder", "Stream", "MemoryStream", "TextReader", "TextWriter"
    };

    private static readonly HashSet<string> CollectionWrapperNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "IEnumerable", "IReadOnlyList", "IReadOnlyCollection", "IList", "List", "ICollection", "Collection",
        "ISet", "HashSet", "IDictionary", "IReadOnlyDictionary", "Dictionary", "Queue", "Stack",
        "Nullable", "IObservable", "IQueryable", "IAsyncEnumerable"
    };

    private static readonly string[] ExcludedEnumSuffixes = ["Status", "Type", "Stage", "Kind", "Mode"];

    private readonly string _filePath;
    private readonly List<FieldDependency> _dependencies = [];
    private string? _currentClass;

    public FieldDependencyExtractor(string filePath = "")
        : base(SyntaxWalkerDepth.Node)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath;
    }

    public IReadOnlyList<FieldDependency> Dependencies => _dependencies.AsReadOnly();

    public static IReadOnlyList<FieldDependency> ExtractFromTree(SyntaxTree tree, string filePath = "")
    {
        ArgumentNullException.ThrowIfNull(tree);

        var path = string.IsNullOrWhiteSpace(filePath) ? tree.FilePath : filePath;
        var extractor = new FieldDependencyExtractor(path);
        extractor.Visit(tree.GetRoot());
        return extractor.Dependencies;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var prevClass = _currentClass;
        _currentClass = node.Identifier.Text;
        base.VisitClassDeclaration(node);
        _currentClass = prevClass;
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var prevClass = _currentClass;
        _currentClass = node.Identifier.Text;
        base.VisitRecordDeclaration(node);
        _currentClass = prevClass;
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (_currentClass is not null)
        {
            var targetTypes = ExtractTargetTypes(node.Declaration.Type);

            foreach (var targetType in targetTypes)
            {
                var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
                var location = new SourceLocation(
                    _filePath,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.EndLinePosition.Line + 1);

                _dependencies.Add(new FieldDependency(
                    SourceClass: _currentClass,
                    TargetType: targetType,
                    Kind: FieldDependencyKind.Field,
                    Location: location,
                    Snippet: node.ToString().Trim(),
                    Confidence: ConfidenceScore.Medium));
            }
        }

        base.VisitFieldDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (_currentClass is not null)
        {
            foreach (var parameter in node.ParameterList.Parameters)
            {
                if (parameter.Type is null) continue;

                var targetTypes = ExtractTargetTypes(parameter.Type);

                foreach (var targetType in targetTypes)
                {
                    var lineSpan = parameter.SyntaxTree.GetLineSpan(parameter.Span);
                    var location = new SourceLocation(
                        _filePath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.EndLinePosition.Line + 1);

                    _dependencies.Add(new FieldDependency(
                        SourceClass: _currentClass,
                        TargetType: targetType,
                        Kind: FieldDependencyKind.ConstructorParameter,
                        Location: location,
                        Snippet: parameter.ToString().Trim(),
                        Confidence: ConfidenceScore.Medium));
                }
            }
        }

        base.VisitConstructorDeclaration(node);
    }

    private static List<string> ExtractTargetTypes(TypeSyntax typeSyntax)
    {
        var results = new List<string>();
        CollectTypes(typeSyntax, results);
        return results;
    }

    private static void CollectTypes(TypeSyntax typeSyntax, List<string> results)
    {
        switch (typeSyntax)
        {
            case GenericNameSyntax genericName:
                var name = genericName.Identifier.Text;
                if (CollectionWrapperNames.Contains(name))
                {
                    // Unwrap collection type arguments: e.g. List<T>, Dictionary<K, V>
                    foreach (var arg in genericName.TypeArgumentList.Arguments)
                    {
                        CollectTypes(arg, results);
                    }
                }
                else if (IsValidDependencyCandidate(name))
                {
                    results.Add(name);
                }
                break;

            case ArrayTypeSyntax arrayType:
                CollectTypes(arrayType.ElementType, results);
                break;

            case NullableTypeSyntax nullableType:
                CollectTypes(nullableType.ElementType, results);
                break;

            case SimpleNameSyntax simpleName:
                var simple = simpleName.Identifier.Text;
                if (IsValidDependencyCandidate(simple))
                {
                    results.Add(simple);
                }
                break;

            case QualifiedNameSyntax qualifiedName:
                CollectTypes(qualifiedName.Right, results);
                break;
        }
    }

    private static bool IsValidDependencyCandidate(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName) || typeName.Length < 2)
        {
            return false;
        }

        // Must start with uppercase letter (PascalCase convention)
        if (!char.IsUpper(typeName[0]))
        {
            return false;
        }

        // Exclude primitive keywords
        if (ExcludedPrimitivesAndKeywords.Contains(typeName))
        {
            return false;
        }

        // Exclude common framework/runtime non-service types
        if (ExcludedFrameworkTypes.Contains(typeName))
        {
            return false;
        }

        // Exclude enum heuristics (ending with Status, Type, Stage, Kind, Mode)
        foreach (var suffix in ExcludedEnumSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal) && typeName.Length > suffix.Length)
            {
                return false;
            }
        }

        return true;
    }
}
