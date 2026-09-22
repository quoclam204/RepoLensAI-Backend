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
    IReadOnlyList<string> AllErrors);

/// <summary>
/// End-to-end repository analysis engine that executes filesystem scanning, project dependency discovery,
/// Roslyn C# parsing, TypeScript/JavaScript analysis, and graph synthesis into a unified AnalysisResult.
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
        Guid analysisJobId = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);

        var effectiveJobId = analysisJobId == Guid.Empty ? Guid.NewGuid() : analysisJobId;
        var allErrors = new List<string>();

        // 1. Scan filesystem safely
        var scanResult = _scanner.Scan(repositoryRootPath);
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
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed reading project file '{proj.RelativePath}': {ex.Message}");
            }
        }

        // 3. Read source code files
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
                }
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed reading source file '{file.RelativePath}': {ex.Message}");
            }
        }

        // 4. Run AST & Dependency Analysis
        var analysis = _projectAnalyzer.Analyze(sourceFileList, csprojList, effectiveJobId);
        allErrors.AddRange(analysis.Errors);

        // 5. Synthesize Top-Level Graph Nodes (Repository, Projects, and Structural Containers)
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
            graphBuilder.AddRelationship(rel);
        }

        // Add project-to-project dependencies from parsed .csproj references
        foreach (var projRef in analysis.ProjectReferences)
        {
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

        // 6. Compute Metrics
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
            AllErrors: allErrors.AsReadOnly());
    }
}
