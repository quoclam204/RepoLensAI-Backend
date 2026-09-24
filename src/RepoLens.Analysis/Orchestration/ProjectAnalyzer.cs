using RepoLens.Analysis.CSharp;
using RepoLens.Analysis.Graph;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.Orchestration;

public sealed record AnalysisResult(
    IReadOnlyList<KnowledgeNode> Nodes,
    IReadOnlyList<KnowledgeRelationship> Relationships,
    IReadOnlyList<ProjectReference> ProjectReferences,
    IReadOnlyList<PackageReference> PackageReferences,
    IReadOnlyList<string> Errors);

/// <summary>
/// Orchestrates multi-file project analysis, resolving cross-file symbols and relationships,
/// and aggregating project manifest dependencies.
/// </summary>
public class ProjectAnalyzer
{
    private readonly CSharpFileAnalyzer _fileAnalyzer;
    private readonly ProjectDependencyExtractor _dependencyExtractor;

    public ProjectAnalyzer(
        CSharpFileAnalyzer? fileAnalyzer = null,
        ProjectDependencyExtractor? dependencyExtractor = null)
    {
        _fileAnalyzer = fileAnalyzer ?? new CSharpFileAnalyzer();
        _dependencyExtractor = dependencyExtractor ?? new ProjectDependencyExtractor();
    }

    public AnalysisResult Analyze(
        IReadOnlyList<(string FilePath, string SourceText)> files,
        IReadOnlyList<(string CsprojPath, string CsprojContent)> csprojFiles,
        Guid analysisJobId = default)
    {
        var effectiveJobId = analysisJobId == Guid.Empty ? Guid.NewGuid() : analysisJobId;
        var errors = new List<string>();
        var projectReferences = new List<ProjectReference>();
        var packageReferences = new List<PackageReference>();

        // 1. Process Project Dependency Manifests (.csproj)
        if (csprojFiles is not null)
        {
            foreach (var (csprojPath, csprojContent) in csprojFiles)
            {
                try
                {
                    var depResult = _dependencyExtractor.Analyze(csprojPath, csprojContent);
                    projectReferences.AddRange(depResult.ProjectReferences);
                    packageReferences.AddRange(depResult.PackageReferences);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error analyzing csproj '{csprojPath}': {ex.Message}");
                }
            }
        }

        if (files is null || files.Count == 0)
        {
            return new AnalysisResult(
                Nodes: [],
                Relationships: [],
                ProjectReferences: projectReferences.AsReadOnly(),
                PackageReferences: packageReferences.AsReadOnly(),
                Errors: errors.AsReadOnly());
        }

        // 2. Pre-scan Pass: Build global symbol dictionary across ALL files
        var globalSymbols = new Dictionary<string, KnowledgeNode>(StringComparer.OrdinalIgnoreCase);
        var fileParser = new CSharpFileParser();

        foreach (var (filePath, sourceText) in files)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) continue;

            try
            {
                var normalizedPath = filePath.Replace('\\', '/');
                var ext = Path.GetExtension(normalizedPath);

                if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var tree = fileParser.ParseText(sourceText, normalizedPath);
                    var rawSymbols = SymbolExtractor.ExtractFromTree(tree, normalizedPath);

                    foreach (var sym in rawSymbols)
                    {
                        var nodeType = sym.Kind switch
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

                        CSharpFileAnalyzer.IndexNode(globalSymbols, node, sym.Namespace, sym.ContainerType);
                    }

                    // Also index Database Entities declared in this file
                    var (_, _, entities) = DatabaseEntityExtractor.ExtractFromTree(tree, normalizedPath);
                    foreach (var ent in entities)
                    {
                        var entityNodeId = $"entity:{ent.EntityName}";
                        var entityNode = KnowledgeNode.Create(
                            id: entityNodeId,
                            name: ent.EntityName,
                            type: KnowledgeNodeType.DatabaseEntity,
                            filePath: normalizedPath,
                            location: ent.Location);

                        globalSymbols[ent.EntityName] = entityNode;
                        globalSymbols[entityNodeId] = entityNode;
                    }
                }
                else if (ext.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".jsx", StringComparison.OrdinalIgnoreCase))
                {
                    var tsExtractor = new TypeScript.TsSymbolExtractor();
                    var (tsSymbols, _) = tsExtractor.ExtractWithImports(sourceText, normalizedPath);

                    foreach (var sym in tsSymbols)
                    {
                        var nodeType = sym.Kind switch
                        {
                            TypeScript.TsSymbolKind.Interface => KnowledgeNodeType.Interface,
                            TypeScript.TsSymbolKind.Class => KnowledgeNodeType.Class,
                            TypeScript.TsSymbolKind.Component => KnowledgeNodeType.Component,
                            TypeScript.TsSymbolKind.Type => KnowledgeNodeType.Struct,
                            TypeScript.TsSymbolKind.Enum => KnowledgeNodeType.Enum,
                            _ => KnowledgeNodeType.Method
                        };

                        var nodeId = $"{nodeType.ToString().ToLowerInvariant()}:{sym.Name}";
                        var node = KnowledgeNode.Create(
                            id: nodeId,
                            name: sym.Name,
                            type: nodeType,
                            filePath: normalizedPath,
                            location: sym.Location);

                        globalSymbols[nodeId] = node;
                        globalSymbols[sym.Name] = node;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error during pre-scan of file '{filePath}': {ex.Message}");
            }
        }

        // 3. Full Analysis Pass: Run analyzers with the global cross-file symbol lookup
        var allNodesMap = new Dictionary<string, KnowledgeNode>(StringComparer.OrdinalIgnoreCase);
        var allRelationships = new List<KnowledgeRelationship>();

        foreach (var (filePath, sourceText) in files)
        {
            var normalizedPath = filePath.Replace('\\', '/');
            var ext = Path.GetExtension(normalizedPath);

            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var fileResult = _fileAnalyzer.Analyze(
                    filePath: filePath,
                    sourceText: sourceText,
                    externalSymbols: globalSymbols,
                    analysisJobId: effectiveJobId);

                foreach (var node in fileResult.Nodes)
                {
                    allNodesMap[node.Id] = node;
                }

                allRelationships.AddRange(fileResult.Relationships);
                errors.AddRange(fileResult.Errors);
            }
            else if (ext.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".jsx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tsExtractor = new TypeScript.TsSymbolExtractor();
                    var (tsSymbols, tsImports) = tsExtractor.ExtractWithImports(sourceText, normalizedPath);

                    foreach (var sym in tsSymbols)
                    {
                        var nodeType = sym.Kind switch
                        {
                            TypeScript.TsSymbolKind.Interface => KnowledgeNodeType.Interface,
                            TypeScript.TsSymbolKind.Class => KnowledgeNodeType.Class,
                            TypeScript.TsSymbolKind.Component => KnowledgeNodeType.Component,
                            TypeScript.TsSymbolKind.Type => KnowledgeNodeType.Struct,
                            TypeScript.TsSymbolKind.Enum => KnowledgeNodeType.Enum,
                            _ => KnowledgeNodeType.Method
                        };

                        var nodeId = $"{nodeType.ToString().ToLowerInvariant()}:{sym.Name}";
                        var node = KnowledgeNode.Create(nodeId, sym.Name, nodeType, normalizedPath, sym.Location);
                        allNodesMap[node.Id] = node;
                    }

                    foreach (var imp in tsImports)
                    {
                        var fileNodeId = $"file:{normalizedPath}";
                        if (!allNodesMap.ContainsKey(fileNodeId))
                        {
                            allNodesMap[fileNodeId] = KnowledgeNode.Create(
                                id: fileNodeId,
                                name: Path.GetFileName(normalizedPath),
                                type: KnowledgeNodeType.File,
                                filePath: normalizedPath);
                        }

                        var targetModule = imp.ModulePath;
                        var targetId = $"module:{targetModule}";
                        if (!allNodesMap.ContainsKey(targetId))
                        {
                            allNodesMap[targetId] = KnowledgeNode.Create(
                                id: targetId,
                                name: targetModule,
                                type: KnowledgeNodeType.File,
                                filePath: targetModule);
                        }

                        var evidence = Evidence.EvidenceFactory.Create(
                            effectiveJobId,
                            normalizedPath,
                            imp.Location.StartLine,
                            imp.Location.EndLine,
                            imp.Snippet,
                            Domain.Enums.EvidenceType.Dependency,
                            ConfidenceScore.Medium,
                            targetModule);

                        allRelationships.Add(KnowledgeRelationship.Create(
                            sourceId: fileNodeId,
                            targetId: targetId,
                            type: KnowledgeRelationshipType.DependsOn,
                            evidence: evidence));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error analyzing web source file '{normalizedPath}': {ex.Message}");
                }
            }
        }

        return new AnalysisResult(
            Nodes: allNodesMap.Values.ToList().AsReadOnly(),
            Relationships: allRelationships.AsReadOnly(),
            ProjectReferences: projectReferences.AsReadOnly(),
            PackageReferences: packageReferences.AsReadOnly(),
            Errors: errors.AsReadOnly());
    }
}
