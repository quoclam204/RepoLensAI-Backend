using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Persistence;
using RepoLens.Application.Models.Architecture;
using RepoLens.Application.Models.Archify;

namespace RepoLens.Application.Services;

/// <summary>
/// Implements the Archify Adapter converting internal architecture models and analysis results
/// into the Archify C4 schema format, preserving all source evidences and semantic relationships.
/// </summary>
public class ArchifyAdapter : IArchifyAdapter
{
    public ArchifyDocument ConvertToArchify(ArchitectureModel architectureModel)
    {
        ArgumentNullException.ThrowIfNull(architectureModel);

        var containers = new List<ArchifyContainer>();
        var componentsByContainer = new Dictionary<string, List<ArchifyComponent>>(StringComparer.OrdinalIgnoreCase);

        // Group component nodes under container nodes
        foreach (var node in architectureModel.Nodes)
        {
            if (node.NodeType == ArchitectureNodeType.Container)
            {
                if (!componentsByContainer.ContainsKey(node.Id))
                {
                    componentsByContainer[node.Id] = new List<ArchifyComponent>();
                }
            }
            else if (node.NodeType is ArchitectureNodeType.Component or ArchitectureNodeType.CodeElement or ArchitectureNodeType.Endpoint)
            {
                var containerId = node.ParentId ?? "default-container";
                if (!componentsByContainer.TryGetValue(containerId, out var compList))
                {
                    compList = new List<ArchifyComponent>();
                    componentsByContainer[containerId] = compList;
                }

                var evidenceList = architectureModel.Evidences
                    .Where(e => e.FilePath.Equals(node.Path, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Id)
                    .ToList();

                compList.Add(new ArchifyComponent(
                    Id: Slugify(node.Id),
                    Name: node.Name,
                    EvidenceIds: evidenceList.AsReadOnly()));
            }
        }

        foreach (var node in architectureModel.Nodes.Where(n => n.NodeType == ArchitectureNodeType.Container))
        {
            componentsByContainer.TryGetValue(node.Id, out var comps);

            containers.Add(new ArchifyContainer(
                Id: Slugify(node.Id),
                Name: node.Name,
                Type: node.Properties.GetValueOrDefault("Type", "Container"),
                Technology: node.Technology ?? ".NET",
                Components: comps != null ? comps.AsReadOnly() : Array.Empty<ArchifyComponent>()));
        }

        var relationships = architectureModel.Relationships.Select(r => new ArchifyRelationship(
            SourceId: Slugify(r.SourceId),
            TargetId: Slugify(r.TargetId),
            Type: r.RelationshipType,
            EvidenceIds: r.EvidenceIds,
            Confidence: r.Confidence)).ToList();

        var system = new ArchifySystem(
            Name: architectureModel.SystemName,
            Description: architectureModel.Description,
            Containers: containers.AsReadOnly(),
            Relationships: relationships.AsReadOnly());

        return new ArchifyDocument(system);
    }

    public ArchifyDocument ConvertFromAnalysis(AnalysisResultModel analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var containers = new List<ArchifyContainer>();
        var containerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Map projects to Archify containers
        foreach (var proj in analysisResult.Projects)
        {
            var containerId = Slugify(proj.Name);
            containerIds.Add(containerId);

            var projComponents = new List<ArchifyComponent>();

            // Find symbols belonging to this project (by project path prefix or ProjectPath)
            var symbolsInProject = analysisResult.CodeSymbols
                .Where(s => !string.IsNullOrWhiteSpace(s.FilePath) &&
                            (s.FilePath.Contains(proj.Name, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrWhiteSpace(proj.Path) && s.FilePath.StartsWith(Path.GetDirectoryName(proj.Path) ?? "", StringComparison.OrdinalIgnoreCase))))
                .Where(s => s.SymbolType is Domain.Enums.SymbolType.Class or Domain.Enums.SymbolType.Interface)
                .ToList();

            foreach (var sym in symbolsInProject)
            {
                var evidenceIds = analysisResult.Evidences
                    .Where(e => e.FilePath.Equals(sym.FilePath, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.EvidenceKey ?? e.Id?.ToString() ?? $"ev:{e.FilePath}:{e.StartLine}")
                    .Distinct()
                    .ToList();

                projComponents.Add(new ArchifyComponent(
                    Id: Slugify(sym.SymbolKey ?? sym.FullName),
                    Name: sym.Name,
                    EvidenceIds: evidenceIds.AsReadOnly()));
            }

            var containerType = InferContainerType(proj.Name, proj.ProjectType);
            var technology = proj.ProjectType.Equals("CSharp", StringComparison.OrdinalIgnoreCase) ? ".NET 10 / C#" : proj.ProjectType;

            containers.Add(new ArchifyContainer(
                Id: containerId,
                Name: proj.Name,
                Type: containerType,
                Technology: technology,
                Components: projComponents.AsReadOnly()));
        }

        // Map relationships
        var relationships = new List<ArchifyRelationship>();
        foreach (var dep in analysisResult.Dependencies)
        {
            var evidenceIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(dep.EvidenceKey))
            {
                evidenceIds.Add(dep.EvidenceKey);
            }
            else if (dep.EvidenceId.HasValue)
            {
                evidenceIds.Add(dep.EvidenceId.Value.ToString());
            }

            var confidence = evidenceIds.Count > 0 ? "confirmed" : "inferred";

            relationships.Add(new ArchifyRelationship(
                SourceId: Slugify(dep.SourceId),
                TargetId: Slugify(dep.TargetId),
                Type: dep.DependencyType.ToString(),
                EvidenceIds: evidenceIds.AsReadOnly(),
                Confidence: confidence));
        }

        var system = new ArchifySystem(
            Name: "RepoLensAnalysisSystem",
            Description: "Evidence-grounded architectural representation generated from static analysis",
            Containers: containers.AsReadOnly(),
            Relationships: relationships.AsReadOnly());

        return new ArchifyDocument(system);
    }

    private static string InferContainerType(string projectName, string projectType)
    {
        if (projectName.EndsWith(".Api", StringComparison.OrdinalIgnoreCase) ||
            projectName.EndsWith(".Web", StringComparison.OrdinalIgnoreCase))
        {
            return "WebApi";
        }

        if (projectName.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase))
        {
            return "InfrastructureService";
        }

        if (projectName.EndsWith(".Application", StringComparison.OrdinalIgnoreCase))
        {
            return "ApplicationCore";
        }

        if (projectName.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase))
        {
            return "DomainModel";
        }

        return "ClassLibrary";
    }

    private static string Slugify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "unknown";
        }

        return input.Trim().ToLowerInvariant()
            .Replace("project:", "")
            .Replace("repo:", "")
            .Replace("class:", "")
            .Replace("interface:", "")
            .Replace('.', '-')
            .Replace('/', '-')
            .Replace('\\', '-');
    }
}
