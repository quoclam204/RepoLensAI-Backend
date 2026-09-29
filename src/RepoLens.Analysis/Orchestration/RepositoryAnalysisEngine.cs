using System.Text;
using RepoLens.Analysis.CSharp;
using RepoLens.Analysis.Evidence;
using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Scanning;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.Orchestration;

public sealed record RepositoryAnalysisResult(
    string RepositoryPath,
    ScannedRepository ScannedMetadata,
    AnalysisResult Analysis,
    IReadOnlyDictionary<string, int> NodeCountByType,
    IReadOnlyDictionary<string, int> RelationshipCountByType,
    IReadOnlyList<string> AllErrors,
    IReadOnlyDictionary<string, string>? FileContents = null);

/// <summary>
/// End-to-end repository analysis engine that executes filesystem scanning, project dependency discovery,
/// Roslyn C# parsing, TypeScript/JavaScript analysis, npm dependency extraction, and graph synthesis.
/// </summary>
public class RepositoryAnalysisEngine
{
    private readonly RepositoryScanner _scanner;
    private readonly ProjectAnalyzer _projectAnalyzer;

    public RepositoryAnalysisEngine(
        RepositoryScanner? scanner = null,
        ProjectAnalyzer? projectAnalyzer = null)
    {
        _scanner = scanner ?? new RepositoryScanner();
        _projectAnalyzer = projectAnalyzer ?? new ProjectAnalyzer();
    }

    public RepositoryAnalysisResult AnalyzeRepository(
        string repositoryRootPath,
        Guid analysisJobId = default,
        AnalysisLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveLimits = limits ?? AnalysisLimits.Default;
        var effectiveJobId = analysisJobId == Guid.Empty ? Guid.NewGuid() : analysisJobId;
        var allErrors = new List<string>();
        var fileContentsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan filesystem safely
        var scanResult = _scanner.Scan(repositoryRootPath, effectiveLimits, cancellationToken);
        allErrors.AddRange(scanResult.ScanErrors);

        // 2. Read .csproj project files
        var csprojList = new List<(string CsprojPath, string CsprojContent)>();
        foreach (var proj in scanResult.Projects.Where(p => p.ProjectType.Equals("CSharp", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (File.Exists(proj.FullPath))
                {
                    var content = File.ReadAllText(proj.FullPath, Encoding.UTF8);
                    csprojList.Add((proj.RelativePath, content));
                    fileContentsMap[proj.RelativePath] = content;
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed reading project file '{proj.RelativePath}': {ex.Message}");
            }
        }

        // 3. Read package.json manifests (T050)
        var packageJsonList = new List<(string PackageJsonPath, string PackageJsonContent)>();
        var seenPackageJsons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var proj in scanResult.Projects.Where(p => p.ProjectType.Equals("Node", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (File.Exists(proj.FullPath) && seenPackageJsons.Add(proj.RelativePath))
                {
                    var content = File.ReadAllText(proj.FullPath, Encoding.UTF8);
                    packageJsonList.Add((proj.RelativePath, content));
                    fileContentsMap[proj.RelativePath] = content;
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed reading package.json '{proj.RelativePath}': {ex.Message}");
            }
        }

        foreach (var cfg in scanResult.ConfigurationFiles.Where(c => Path.GetFileName(c.RelativePath).Equals("package.json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (File.Exists(cfg.FullPath) && seenPackageJsons.Add(cfg.RelativePath))
                {
                    var content = File.ReadAllText(cfg.FullPath, Encoding.UTF8);
                    packageJsonList.Add((cfg.RelativePath, content));
                    fileContentsMap[cfg.RelativePath] = content;
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed reading package.json '{cfg.RelativePath}': {ex.Message}");
            }
        }

        // 4. Read source code & documentation files
        var sourceFileList = new List<(string FilePath, string SourceText)>();
        foreach (var file in scanResult.SourceFiles)
        {
            try
            {
                if (File.Exists(file.FullPath))
                {
                    // Skip files exceeding 5MB to avoid out-of-memory on accidental large files
                    if (file.SizeInBytes > 5 * 1024 * 1024)
                    {
                        allErrors.Add($"Skipping excessively large file '{file.RelativePath}' ({file.SizeInBytes} bytes)");
                        continue;
                    }

                    var content = File.ReadAllText(file.FullPath, Encoding.UTF8);
                    sourceFileList.Add((file.RelativePath, content));
                    fileContentsMap[file.RelativePath] = content;
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed reading source file '{file.RelativePath}': {ex.Message}");
            }
        }

        // 5. Run AST & Dependency Analysis
        var analysis = _projectAnalyzer.Analyze(sourceFileList, csprojList, packageJsonList, effectiveJobId);
        allErrors.AddRange(analysis.Errors);

        // 6. Synthesize Top-Level Graph Nodes (Repository, Projects, and Structural Containers)
        var graphBuilder = new KnowledgeGraphBuilder();

        // Add root repository node
        var repoName = Path.GetFileName(Path.TrimEndingDirectorySeparator(scanResult.RootPath));
        if (string.IsNullOrWhiteSpace(repoName)) repoName = "RepositoryRoot";

        var repoNode = KnowledgeNode.Create(
            id: "repo:root",
            name: repoName,
            type: KnowledgeNodeType.Repository,
            filePath: "");

        graphBuilder.AddNode(repoNode);

        // Add discovered project nodes & link to repository
        foreach (var proj in scanResult.Projects)
        {
            var projNodeId = $"project:{proj.ProjectName}";
            var projNode = KnowledgeNode.Create(
                id: projNodeId,
                name: proj.ProjectName,
                type: KnowledgeNodeType.Project,
                filePath: proj.RelativePath,
                properties: new Dictionary<string, string>
                {
                    ["ProjectType"] = proj.ProjectType
                });

            graphBuilder.AddNode(projNode);

            var repoContainsProjEvidence = EvidenceFactory.Create(
                effectiveJobId,
                proj.RelativePath,
                1,
                1,
                $"Project: {proj.ProjectName}",
                EvidenceType.Configuration,
                ConfidenceScore.High,
                proj.ProjectName);

            graphBuilder.AddRelationship(KnowledgeRelationship.Create(
                sourceId: repoNode.Id,
                targetId: projNode.Id,
                type: KnowledgeRelationshipType.Contains,
                evidence: repoContainsProjEvidence));
        }

        // Add all analyzed nodes
        foreach (var node in analysis.Nodes)
        {
            graphBuilder.AddNode(node);
        }

        // Add all analyzed relationships
        foreach (var rel in analysis.Relationships)
        {
            if (graphBuilder.Relationships.Count >= effectiveLimits.MaxRelationships)
            {
                allErrors.Add($"Maximum graph relationships limit reached ({effectiveLimits.MaxRelationships}). Capping further relationship extraction.");
                break;
            }
            graphBuilder.AddRelationship(rel);
        }

        // Add project-to-project dependencies from parsed .csproj references
        foreach (var projRef in analysis.ProjectReferences)
        {
            if (graphBuilder.Relationships.Count >= effectiveLimits.MaxRelationships)
            {
                allErrors.Add($"Maximum graph relationships limit reached ({effectiveLimits.MaxRelationships}). Capping further relationship extraction.");
                break;
            }

            var sourceProjName = scanResult.Projects
                .FirstOrDefault(p => projRef.Location.FilePath.Contains(p.ProjectName, StringComparison.OrdinalIgnoreCase))?.ProjectName;

            if (sourceProjName is not null)
            {
                var sourceId = $"project:{sourceProjName}";
                var targetId = $"project:{projRef.TargetProjectName}";

                // Ensure target project node exists
                if (!graphBuilder.TryGetNode(targetId, out _))
                {
                    graphBuilder.AddNode(KnowledgeNode.Create(
                        id: targetId,
                        name: projRef.TargetProjectName,
                        type: KnowledgeNodeType.Project,
                        filePath: projRef.RelativePath));
                }

                var evidence = EvidenceFactory.Create(
                    effectiveJobId,
                    projRef.Location.FilePath,
                    projRef.Location.StartLine,
                    projRef.Location.EndLine,
                    projRef.Snippet,
                    EvidenceType.Dependency,
                    ConfidenceScore.High,
                    projRef.TargetProjectName);

                graphBuilder.AddRelationship(KnowledgeRelationship.Create(
                    sourceId: sourceId,
                    targetId: targetId,
                    type: KnowledgeRelationshipType.DependsOn,
                    evidence: evidence));
            }
        }

        // Add package dependency relationships (npm and NuGet)
        foreach (var pkgRef in analysis.PackageReferences)
        {
            if (graphBuilder.Relationships.Count >= effectiveLimits.MaxRelationships)
            {
                allErrors.Add($"Maximum graph relationships limit reached ({effectiveLimits.MaxRelationships}). Capping further relationship extraction.");
                break;
            }

            var sourceProj = scanResult.Projects
                .FirstOrDefault(p => pkgRef.Location.FilePath.StartsWith(Path.GetDirectoryName(p.RelativePath) ?? "", StringComparison.OrdinalIgnoreCase));

            if (sourceProj is not null)
            {
                var sourceId = $"project:{sourceProj.ProjectName}";
                var targetPkgId = $"package:{pkgRef.PackageName}";

                if (!graphBuilder.TryGetNode(targetPkgId, out _))
                {
                    graphBuilder.AddNode(KnowledgeNode.Create(
                        id: targetPkgId,
                        name: pkgRef.PackageName,
                        type: KnowledgeNodeType.Service,
                        filePath: pkgRef.Location.FilePath,
                        properties: new Dictionary<string, string>
                        {
                            ["Version"] = pkgRef.Version ?? ""
                        }));
                }

                var evidence = EvidenceFactory.Create(
                    effectiveJobId,
                    pkgRef.Location.FilePath,
                    pkgRef.Location.StartLine,
                    pkgRef.Location.EndLine,
                    pkgRef.Snippet,
                    EvidenceType.Dependency,
                    ConfidenceScore.High,
                    pkgRef.PackageName);

                graphBuilder.AddRelationship(KnowledgeRelationship.Create(
                    sourceId: sourceId,
                    targetId: targetPkgId,
                    type: KnowledgeRelationshipType.DependsOn,
                    evidence: evidence));
            }
        }

        // 7. Compute Metrics
        var allNodes = graphBuilder.Nodes;
        var allRelationships = graphBuilder.Relationships;

        var nodeCountByType = allNodes
            .GroupBy(n => n.Type.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var relCountByType = allRelationships
            .GroupBy(r => r.Type.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var finalAnalysis = new AnalysisResult(
            Nodes: allNodes.ToList().AsReadOnly(),
            Relationships: allRelationships.ToList().AsReadOnly(),
            ProjectReferences: analysis.ProjectReferences,
            PackageReferences: analysis.PackageReferences,
            Errors: allErrors.AsReadOnly());

        return new RepositoryAnalysisResult(
            RepositoryPath: scanResult.RootPath,
            ScannedMetadata: scanResult,
            Analysis: finalAnalysis,
            NodeCountByType: nodeCountByType,
            RelationshipCountByType: relCountByType,
            AllErrors: allErrors.AsReadOnly(),
            FileContents: fileContentsMap);
    }

    /// <summary>
    /// Asynchronously analyzes a repository without executing target code, supporting cancellation.
    /// </summary>
    public async Task<RepositoryAnalysisResult> AnalyzeRepositoryAsync(
        string repositoryRootPath,
        Guid analysisJobId = default,
        AnalysisLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => AnalyzeRepository(repositoryRootPath, analysisJobId, limits, cancellationToken), cancellationToken);
    }
}
