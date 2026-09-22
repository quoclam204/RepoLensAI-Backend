using Microsoft.CodeAnalysis;
using RepoLens.Analysis.CSharp;
using RepoLens.Analysis.Evidence;
using RepoLens.Analysis.Graph;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.Orchestration;

public sealed record AnalysisFileResult(
    IReadOnlyList<KnowledgeNode> Nodes,
    IReadOnlyList<KnowledgeRelationship> Relationships,
    IReadOnlyList<string> Errors);

/// <summary>
/// Analyzes a single C# source file, running all AST extractors and assembling local nodes and relationships.
/// </summary>
public class CSharpFileAnalyzer
{
    private readonly CSharpFileParser _parser;

    public CSharpFileAnalyzer(CSharpFileParser? parser = null)
    {
        _parser = parser ?? new CSharpFileParser();
    }

    public AnalysisFileResult Analyze(
        string filePath,
        string sourceText,
        IReadOnlyDictionary<string, KnowledgeNode>? externalSymbols = null,
        Guid analysisJobId = default)
    {
        var effectiveJobId = analysisJobId == Guid.Empty ? Guid.NewGuid() : analysisJobId;
        var normalizedPath = string.IsNullOrWhiteSpace(filePath) ? "unknown.cs" : filePath.Replace('\\', '/');
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new AnalysisFileResult([], [], []);
        }

        try
        {
            var syntaxTree = _parser.ParseText(sourceText, normalizedPath);
            var nodes = new List<KnowledgeNode>();
            var relationships = new List<KnowledgeRelationship>();

            // 1. Symbol Extraction & KnowledgeNode creation
            var rawSymbols = SymbolExtractor.ExtractFromTree(syntaxTree, normalizedPath);
            var localSymbolMap = new Dictionary<string, KnowledgeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var sym in rawSymbols)
            {
                var nodeType = MapSymbolKind(sym.Kind);
                var nodeId = $"{nodeType.ToString().ToLowerInvariant()}:{sym.Name}";

                var node = KnowledgeNode.Create(
                    id: nodeId,
                    name: sym.Name,
                    type: nodeType,
                    filePath: normalizedPath,
                    location: sym.Location);

                if (sym.Namespace is not null)
                {
                    node.Properties["Namespace"] = sym.Namespace;
                }
                if (sym.ContainerType is not null)
                {
                    node.Properties["ContainerType"] = sym.ContainerType;
                }

                nodes.Add(node);
                IndexNode(localSymbolMap, node, sym.Namespace, sym.ContainerType);
            }

            // Create CONTAINS relationships for nested members (methods/properties in class/interface)
            foreach (var sym in rawSymbols)
            {
                if (sym.ContainerType is not null &&
                    localSymbolMap.TryGetValue(sym.ContainerType, out var containerNode))
                {
                    var memberId = $"{MapSymbolKind(sym.Kind).ToString().ToLowerInvariant()}:{sym.Name}";
                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        sym.Location.StartLine,
                        sym.Location.EndLine,
                        $"{sym.ContainerType}.{sym.Name}",
                        EvidenceType.Declaration,
                        ConfidenceScore.Exact,
                        sym.Name);

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: containerNode.Id,
                        targetId: memberId,
                        type: KnowledgeRelationshipType.Contains,
                        evidence: evidence));
                }
            }

            // Combined symbol map: includes external/global symbols if provided, giving precedence to local symbols
            var lookupMap = new Dictionary<string, KnowledgeNode>(StringComparer.OrdinalIgnoreCase);
            if (externalSymbols is not null)
            {
                foreach (var kvp in externalSymbols)
                {
                    lookupMap[kvp.Key] = kvp.Value;
                }
            }
            foreach (var kvp in localSymbolMap)
            {
                lookupMap[kvp.Key] = kvp.Value;
            }

            // 2. Inheritance & Implementation
            var inheritances = InheritanceExtractor.ExtractFromTree(syntaxTree, normalizedPath);
            foreach (var inh in inheritances)
            {
                if (lookupMap.TryGetValue(inh.DerivedType, out var sourceNode) &&
                    lookupMap.TryGetValue(inh.BaseType, out var targetNode))
                {
                    var relType = inh.IsInterfaceHeuristic
                        ? KnowledgeRelationshipType.Implements
                        : KnowledgeRelationshipType.Inherits;

                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        inh.Location.StartLine,
                        inh.Location.EndLine,
                        inh.Snippet,
                        EvidenceType.Declaration,
                        ConfidenceScore.High,
                        inh.DerivedType);

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: sourceNode.Id,
                        targetId: targetNode.Id,
                        type: relType,
                        evidence: evidence));
                }
            }

            // 3. Method Calls
            var calls = CallGraphExtractor.ExtractFromTree(syntaxTree, normalizedPath);
            foreach (var call in calls)
            {
                var callerLookup = call.CallerSymbol.Split('.').Last();
                if ((lookupMap.TryGetValue(call.CallerSymbol, out var sourceNode) ||
                     lookupMap.TryGetValue(callerLookup, out sourceNode)) &&
                    lookupMap.TryGetValue(call.CalleeName, out var targetNode))
                {
                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        call.Location.StartLine,
                        call.Location.EndLine,
                        call.Snippet,
                        EvidenceType.Invocation,
                        ConfidenceScore.Medium,
                        call.CalleeName);

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: sourceNode.Id,
                        targetId: targetNode.Id,
                        type: KnowledgeRelationshipType.Calls,
                        evidence: evidence));
                }
            }

            // 4. Field & Constructor Dependencies
            var fieldDeps = FieldDependencyExtractor.ExtractFromTree(syntaxTree, normalizedPath);
            foreach (var dep in fieldDeps)
            {
                if (lookupMap.TryGetValue(dep.SourceClass, out var sourceNode) &&
                    lookupMap.TryGetValue(dep.TargetType, out var targetNode))
                {
                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        dep.Location.StartLine,
                        dep.Location.EndLine,
                        dep.Snippet,
                        EvidenceType.Dependency,
                        ConfidenceScore.Medium,
                        dep.TargetType);

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: sourceNode.Id,
                        targetId: targetNode.Id,
                        type: KnowledgeRelationshipType.DependsOn,
                        evidence: evidence));
                }
            }

            // 5. API Endpoints
            var endpoints = ApiEndpointExtractor.ExtractFromTree(syntaxTree, normalizedPath);
            foreach (var ep in endpoints)
            {
                var endpointNodeId = $"endpoint:{ep.HttpMethod.ToLowerInvariant()}:{ep.RouteTemplate}";
                var endpointNode = KnowledgeNode.Create(
                    id: endpointNodeId,
                    name: $"{ep.HttpMethod} {ep.RouteTemplate}",
                    type: KnowledgeNodeType.Endpoint,
                    filePath: normalizedPath,
                    location: ep.Location,
                    properties: new Dictionary<string, string>
                    {
                        ["HttpMethod"] = ep.HttpMethod,
                        ["RouteTemplate"] = ep.RouteTemplate,
                        ["HandlerSymbol"] = ep.HandlerSymbol
                    });

                nodes.Add(endpointNode);

                var controllerName = ep.HandlerSymbol.Split('.').First();
                if (lookupMap.TryGetValue(controllerName, out var controllerNode) ||
                    lookupMap.TryGetValue(ep.HandlerSymbol, out controllerNode))
                {
                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        ep.Location.StartLine,
                        ep.Location.EndLine,
                        ep.Snippet,
                        EvidenceType.Route,
                        ConfidenceScore.High,
                        ep.RouteTemplate);

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: controllerNode.Id,
                        targetId: endpointNode.Id,
                        type: KnowledgeRelationshipType.Exposes,
                        evidence: evidence));
                }
            }

            // 6. Database Entities, DbSets, and Relationships
            var (contexts, dbSets, entities, dbRelationships) = DatabaseEntityExtractor.ExtractAllFromTree(syntaxTree, normalizedPath);

            foreach (var ent in entities)
            {
                var entityNodeId = $"entity:{ent.EntityName}";
                var entityNode = KnowledgeNode.Create(
                    id: entityNodeId,
                    name: ent.EntityName,
                    type: KnowledgeNodeType.DatabaseEntity,
                    filePath: normalizedPath,
                    location: ent.Location,
                    properties: new Dictionary<string, string>
                    {
                        ["TableName"] = ent.TableName ?? ent.EntityName,
                        ["Keys"] = string.Join(",", ent.KeyProperties)
                    });

                nodes.Add(entityNode);
                lookupMap[ent.EntityName] = entityNode;
                lookupMap[entityNodeId] = entityNode;
            }

            foreach (var dbSet in dbSets)
            {
                KnowledgeNode? targetEntityNode = null;
                if (lookupMap.TryGetValue($"entity:{dbSet.EntityTypeName}", out var entNode))
                {
                    targetEntityNode = entNode;
                }
                else if (lookupMap.TryGetValue(dbSet.EntityTypeName, out var anyNode))
                {
                    targetEntityNode = anyNode;
                }

                if (lookupMap.TryGetValue(dbSet.ContainingContextName, out var contextNode) &&
                    targetEntityNode is not null)
                {
                    var targetId = targetEntityNode.Type == KnowledgeNodeType.DatabaseEntity
                        ? targetEntityNode.Id
                        : $"entity:{targetEntityNode.Name}";

                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        dbSet.Location.StartLine,
                        dbSet.Location.EndLine,
                        dbSet.Snippet,
                        EvidenceType.Database,
                        ConfidenceScore.High,
                        dbSet.EntityTypeName);

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: contextNode.Id,
                        targetId: targetId,
                        type: KnowledgeRelationshipType.MapsTo,
                        evidence: evidence));
                }
            }

            foreach (var rel in dbRelationships)
            {
                if (lookupMap.TryGetValue(rel.PrincipalEntity, out var principalNode) &&
                    lookupMap.TryGetValue(rel.DependentEntity, out var dependentNode))
                {
                    var evidence = EvidenceFactory.Create(
                        effectiveJobId,
                        normalizedPath,
                        rel.Location.StartLine,
                        rel.Location.EndLine,
                        rel.Snippet,
                        EvidenceType.Database,
                        ConfidenceScore.High,
                        $"{rel.PrincipalEntity}->{rel.DependentEntity}");

                    relationships.Add(KnowledgeRelationship.Create(
                        sourceId: principalNode.Id,
                        targetId: dependentNode.Id,
                        type: KnowledgeRelationshipType.DependsOn,
                        evidence: evidence,
                        properties: new Dictionary<string, string>
                        {
                            ["Multiplicity"] = rel.Multiplicity,
                            ["ForeignKey"] = rel.ForeignKey ?? ""
                        }));
                }
            }

            return new AnalysisFileResult(nodes.AsReadOnly(), relationships.AsReadOnly(), errors.AsReadOnly());
        }
        catch (Exception ex)
        {
            // Defensive: do not crash on single file failure
            errors.Add($"Error analyzing file '{normalizedPath}': {ex.Message}");
            return new AnalysisFileResult([], [], errors.AsReadOnly());
        }
    }

    public static void IndexNode(
        Dictionary<string, KnowledgeNode> map,
        KnowledgeNode node,
        string? ns = null,
        string? container = null)
    {
        SetWithPrecedence(map, node.Id, node);
        SetWithPrecedence(map, node.Name, node);

        if (container is not null)
        {
            SetWithPrecedence(map, $"{container}.{node.Name}", node);
        }

        if (ns is not null)
        {
            SetWithPrecedence(map, $"{ns}.{node.Name}", node);
            if (container is not null)
            {
                SetWithPrecedence(map, $"{ns}.{container}.{node.Name}", node);
            }
        }
    }

    private static void SetWithPrecedence(Dictionary<string, KnowledgeNode> map, string key, KnowledgeNode node)
    {
        if (map.TryGetValue(key, out var existing))
        {
            // If existing is a type (Class, Interface, Record, Struct, Enum) and incoming is not, preserve the type
            if (IsTypeNode(existing.Type) && !IsTypeNode(node.Type))
            {
                return;
            }
        }

        map[key] = node;
    }

    private static bool IsTypeNode(KnowledgeNodeType type) =>
        type is KnowledgeNodeType.Class
            or KnowledgeNodeType.Interface
            or KnowledgeNodeType.Struct
            or KnowledgeNodeType.Record
            or KnowledgeNodeType.Enum;

    private static KnowledgeNodeType MapSymbolKind(CSharpSymbolKind kind) => kind switch
    {
        CSharpSymbolKind.Interface => KnowledgeNodeType.Interface,
        CSharpSymbolKind.Class => KnowledgeNodeType.Class,
        CSharpSymbolKind.Struct => KnowledgeNodeType.Struct,
        CSharpSymbolKind.Record => KnowledgeNodeType.Record,
        CSharpSymbolKind.Enum => KnowledgeNodeType.Enum,
        CSharpSymbolKind.Method => KnowledgeNodeType.Method,
        CSharpSymbolKind.Constructor => KnowledgeNodeType.Method,
        CSharpSymbolKind.Property => KnowledgeNodeType.Property,
        CSharpSymbolKind.Field => KnowledgeNodeType.Property,
        _ => KnowledgeNodeType.Class
    };
}
