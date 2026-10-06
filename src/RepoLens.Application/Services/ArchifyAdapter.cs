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

    public ArchifyDocument ConvertFromArchitectureResponse(DTOs.Architecture.ArchitectureResponse architectureResponse)
    {
        ArgumentNullException.ThrowIfNull(architectureResponse);

        var containers = new List<ArchifyContainer>();
        var containerNodes = architectureResponse.Nodes
            .Where(n => string.Equals(n.Type, "Project", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n.Type, "Container", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (containerNodes.Count == 0)
        {
            containerNodes.Add(new DTOs.Architecture.ArchitectureNodeDto(
                Id: "default-container",
                Type: "Container",
                Name: "MainApplication",
                Path: ""));
        }

        foreach (var cNode in containerNodes)
        {
            var containerId = Slugify(cNode.Name);
            var compNodes = architectureResponse.Nodes
                .Where(n => !string.Equals(n.Type, "Project", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(n.Type, "Container", StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrEmpty(cNode.Path) ||
                             n.Path.StartsWith(cNode.Path, StringComparison.OrdinalIgnoreCase) ||
                             n.Path.Contains(cNode.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var components = compNodes.Select(cn => new ArchifyComponent(
                Id: Slugify(cn.Name),
                Name: cn.Name,
                EvidenceIds: Array.Empty<string>())).ToList();

            containers.Add(new ArchifyContainer(
                Id: containerId,
                Name: cNode.Name,
                Type: InferContainerType(cNode.Name, cNode.Type),
                Technology: ".NET 10 / C#",
                Components: components.AsReadOnly()));
        }

        var relationships = architectureResponse.Edges.Select(e =>
        {
            var evidenceIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(e.EvidenceId))
            {
                evidenceIds.Add(e.EvidenceId);
            }
            else if (e.Evidence != null)
            {
                evidenceIds.Add($"ev:{e.Evidence.File}:{e.Evidence.StartLine}-{e.Evidence.EndLine}");
            }

            return new ArchifyRelationship(
                SourceId: Slugify(e.Source),
                TargetId: Slugify(e.Target),
                Type: e.Type,
                EvidenceIds: evidenceIds.AsReadOnly(),
                Confidence: string.IsNullOrWhiteSpace(e.Confidence) ? "confirmed" : e.Confidence);
        }).ToList();

        var system = new ArchifySystem(
            Name: "RepoLensAnalysisSystem",
            Description: $"Evidence-grounded architectural representation for analysis {architectureResponse.AnalysisId}",
            Containers: containers.AsReadOnly(),
            Relationships: relationships.AsReadOnly());

        return new ArchifyDocument(system);
    }

    public ArchifyV3Document ConvertToArchifyV3(DTOs.Architecture.ArchitectureResponse architectureResponse, string? systemTitle = null)
    {
        ArgumentNullException.ThrowIfNull(architectureResponse);

        var components = new List<ArchifyV3Component>();
        var uiIds = new List<string>();
        var runtimeIds = new List<string>();
        var policyIds = new List<string>();
        var dataIds = new List<string>();
        var externalIds = new List<string>();

        foreach (var node in architectureResponse.Nodes)
        {
            var id = Slugify(node.Name);
            var category = CategorizeNode(node.Name, node.Type, node.Path);
            var icon = CategoryToIcon(category);
            var tag = InferTag(node.Name, node.Type);
            var sublabel = node.Type;

            components.Add(new ArchifyV3Component(
                Id: id,
                Type: category,
                Label: node.Name,
                Sublabel: sublabel,
                Tag: tag,
                Icon: icon,
                Category: category,
                Sources: !string.IsNullOrWhiteSpace(node.Path) ? new[] { node.Path } : null));

            switch (category)
            {
                case "ui":
                    uiIds.Add(id);
                    break;
                case "runtime":
                    runtimeIds.Add(id);
                    break;
                case "policy":
                    policyIds.Add(id);
                    break;
                case "data":
                    dataIds.Add(id);
                    break;
                case "external":
                    externalIds.Add(id);
                    break;
                default:
                    runtimeIds.Add(id);
                    break;
            }
        }

        var boundaries = new List<ArchifyV3Boundary>();
        if (uiIds.Count > 0)
        {
            boundaries.Add(new ArchifyV3Boundary("lane-ui", "region", "01 / User Interface & Gateway", "ui", uiIds.AsReadOnly()));
        }
        if (policyIds.Count > 0)
        {
            boundaries.Add(new ArchifyV3Boundary("lane-policy", "region", "EX / Policy, Guard & Gate", "policy", policyIds.AsReadOnly()));
        }
        if (runtimeIds.Count > 0)
        {
            boundaries.Add(new ArchifyV3Boundary("lane-runtime", "region", "02 / Core Runtime & Application Services", "runtime", runtimeIds.AsReadOnly()));
        }
        if (dataIds.Count > 0)
        {
            boundaries.Add(new ArchifyV3Boundary("lane-data", "region", "03 / Data, Persistence & Storage", "data", dataIds.AsReadOnly()));
        }
        if (externalIds.Count > 0)
        {
            boundaries.Add(new ArchifyV3Boundary("lane-external", "region", "04 / External Services & Cloud APIs", "external", externalIds.AsReadOnly()));
        }

        var connections = architectureResponse.Edges.Select((edge, idx) => new ArchifyV3Connection(
            Id: !string.IsNullOrWhiteSpace(edge.Id) ? edge.Id : $"conn-{idx}",
            From: Slugify(edge.Source),
            To: Slugify(edge.Target),
            Label: edge.Type,
            Variant: "primary",
            EvidenceId: edge.EvidenceId,
            Confidence: edge.Confidence)).ToList();

        var meta = new ArchifyV3Meta(
            Title: !string.IsNullOrWhiteSpace(systemTitle) ? systemTitle : "RepoLens Architecture Map",
            Subtitle: $"Analysis {architectureResponse.AnalysisId} • {components.Count} components • {connections.Count} links",
            Animation: "trace",
            QualityProfile: "showcase");

        return new ArchifyV3Document(
            SchemaVersion: 1,
            DiagramType: "architecture",
            Meta: meta,
            Components: components.AsReadOnly(),
            Boundaries: boundaries.AsReadOnly(),
            Connections: connections.AsReadOnly());
    }

    public string GenerateStandaloneHtml(ArchifyV3Document doc, string theme = "dark")
    {
        ArgumentNullException.ThrowIfNull(doc);
        var isDark = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase);
        var bg = isDark ? "#060b14" : "#f8fafc";
        var text = isDark ? "#f1f5f9" : "#0f172a";
        var cardBg = isDark ? "#09101d" : "#ffffff";
        var borderColor = isDark ? "#1e293b" : "#cbd5e1";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>{System.Net.WebUtility.HtmlEncode(doc.Meta.Title)} - Archify</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine($"    :root {{ --bg: {bg}; --text: {text}; --card-bg: {cardBg}; --border: {borderColor}; }}");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'JetBrains Mono', monospace; background: var(--bg); color: var(--text); overflow: hidden; height: 100vh; display: flex; flex-direction: column; }");
        sb.AppendLine("    header { background: var(--card-bg); border-bottom: 1px solid var(--border); padding: 12px 20px; display: flex; justify-content: space-between; align-items: center; }");
        sb.AppendLine("    .title { font-weight: 700; font-size: 15px; display: flex; align-items: center; gap: 10px; }");
        sb.AppendLine("    .badge { font-size: 11px; padding: 3px 8px; border-radius: 6px; background: rgba(0,240,255,0.12); color: #00f0ff; border: 1px solid rgba(0,240,255,0.3); }");
        sb.AppendLine("    .toolbar { display: flex; gap: 8px; align-items: center; }");
        sb.AppendLine("    .btn { background: var(--card-bg); border: 1px solid var(--border); color: var(--text); padding: 6px 12px; border-radius: 6px; font-size: 12px; cursor: pointer; transition: all 0.2s; }");
        sb.AppendLine("    .btn:hover { border-color: #00f0ff; color: #00f0ff; }");
        sb.AppendLine("    main { flex: 1; position: relative; overflow: hidden; }");
        sb.AppendLine("    svg { width: 100%; height: 100%; cursor: grab; }");
        sb.AppendLine("    svg:active { cursor: grabbing; }");
        sb.AppendLine("    .node { cursor: pointer; transition: filter 0.2s, opacity 0.2s; }");
        sb.AppendLine("    .node.active { filter: drop-shadow(0 0 12px #00f0ff); }");
        sb.AppendLine("    .node.dimmed { opacity: 0.15; }");
        sb.AppendLine("    .edge { stroke: #38bdf8; stroke-width: 2; transition: opacity 0.2s; }");
        sb.AppendLine("    .edge.dimmed { opacity: 0.1; }");
        sb.AppendLine("    .hud { position: absolute; top: 16px; right: 16px; width: 280px; background: rgba(9,16,29,0.92); backdrop-filter: blur(12px); border: 1px solid var(--border); border-radius: 10px; padding: 14px; font-size: 12px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header>");
        sb.AppendLine($"    <div class=\"title\"><span>Archify</span><span class=\"badge\">C4 Architecture</span><span>{System.Net.WebUtility.HtmlEncode(doc.Meta.Title)}</span></div>");
        sb.AppendLine("    <div class=\"toolbar\">");
        sb.AppendLine("      <button class=\"btn\" onclick=\"resetCamera()\">Reset View</button>");
        sb.AppendLine("      <button class=\"btn\" onclick=\"clearFocus()\">Clear Focus</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <main>");
        sb.AppendLine("    <svg id=\"canvas\" viewBox=\"0 0 1200 800\">");
        sb.AppendLine("      <defs>");
        sb.AppendLine("        <marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"8\" refY=\"5\" markerWidth=\"6\" markerHeight=\"6\" orient=\"auto-start-reverse\">");
        sb.AppendLine("          <path d=\"M 0 1 L 10 5 L 0 9 z\" fill=\"#38bdf8\" />");
        sb.AppendLine("        </marker>");
        sb.AppendLine("      </defs>");
        sb.AppendLine("      <g id=\"viewport\">");

        // Layout nodes in a clean grid
        var compPositions = new Dictionary<string, (int x, int y)>();
        var curY = 80;
        foreach (var b in doc.Boundaries)
        {
            sb.AppendLine($"        <rect x=\"40\" y=\"{curY}\" width=\"1120\" height=\"130\" rx=\"8\" fill=\"rgba(56,189,248,0.03)\" stroke=\"rgba(56,189,248,0.2)\" stroke-dasharray=\"4 4\" />");
            sb.AppendLine($"        <text x=\"60\" y=\"{curY + 22}\" fill=\"#38bdf8\" font-size=\"12\" font-weight=\"bold\">{System.Net.WebUtility.HtmlEncode(b.Label)}</text>");

            var curX = 60;
            foreach (var wId in b.Wraps)
            {
                compPositions[wId] = (curX, curY + 35);
                curX += 220;
            }
            curY += 150;
        }

        // Draw connections
        foreach (var conn in doc.Connections)
        {
            if (compPositions.TryGetValue(conn.From, out var p1) && compPositions.TryGetValue(conn.To, out var p2))
            {
                var x1 = p1.x + 90;
                var y1 = p1.y + 30;
                var x2 = p2.x + 90;
                var y2 = p2.y + 30;
                sb.AppendLine($"        <line class=\"edge\" data-from=\"{conn.From}\" data-to=\"{conn.To}\" x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" marker-end=\"url(#arrow)\" />");
            }
        }

        // Draw nodes
        foreach (var c in doc.Components)
        {
            if (!compPositions.TryGetValue(c.Id, out var pos))
            {
                pos = (60, curY);
                curY += 80;
            }
            sb.AppendLine($"        <g class=\"node\" id=\"node-{c.Id}\" data-id=\"{c.Id}\" data-label=\"{System.Net.WebUtility.HtmlEncode(c.Label)}\" data-cat=\"{c.Category}\" onclick=\"selectNode('{c.Id}')\">");
            sb.AppendLine($"          <rect x=\"{pos.x}\" y=\"{pos.y}\" width=\"180\" height=\"60\" rx=\"8\" fill=\"#09101d\" stroke=\"#00f0ff\" stroke-width=\"1.5\" />");
            sb.AppendLine($"          <text x=\"{pos.x + 12}\" y=\"{pos.y + 26}\" fill=\"#ffffff\" font-size=\"12\" font-weight=\"bold\">{System.Net.WebUtility.HtmlEncode(c.Label)}</text>");
            sb.AppendLine($"          <text x=\"{pos.x + 12}\" y=\"{pos.y + 44}\" fill=\"#38bdf8\" font-size=\"10\">{System.Net.WebUtility.HtmlEncode(c.Sublabel ?? c.Type)}</text>");
            sb.AppendLine("        </g>");
        }

        sb.AppendLine("      </g>");
        sb.AppendLine("    </svg>");
        sb.AppendLine("    <div class=\"hud\" id=\"hud\">");
        sb.AppendLine("      <h4 id=\"hud-title\" style=\"color:#00f0ff;margin-bottom:8px;\">System Overview</h4>");
        sb.AppendLine($"      <p id=\"hud-body\" style=\"color:#94a3b8;line-height:1.5;\">{doc.Components.Count} components analyzed with full code evidence.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    let selectedId = null;");
        sb.AppendLine("    function selectNode(id) {");
        sb.AppendLine("      selectedId = id;");
        sb.AppendLine("      const nodes = document.querySelectorAll('.node');");
        sb.AppendLine("      const edges = document.querySelectorAll('.edge');");
        sb.AppendLine("      const activeEdges = [];");
        sb.AppendLine("      edges.forEach(e => {");
        sb.AppendLine("        const matches = e.dataset.from === id || e.dataset.to === id;");
        sb.AppendLine("        if (matches) activeEdges.push(e);");
        sb.AppendLine("        e.classList.toggle('dimmed', !matches);");
        sb.AppendLine("      });");
        sb.AppendLine("      nodes.forEach(n => {");
        sb.AppendLine("        const isTarget = n.dataset.id === id;");
        sb.AppendLine("        const isConn = activeEdges.some(e => e.dataset.from === n.dataset.id || e.dataset.to === n.dataset.id);");
        sb.AppendLine("        n.classList.toggle('active', isTarget);");
        sb.AppendLine("        n.classList.toggle('dimmed', !isTarget && !isConn);");
        sb.AppendLine("      });");
        sb.AppendLine("      const targetNode = document.querySelector(`.node[data-id=\"${id}\"]`);");
        sb.AppendLine("      if (targetNode) {");
        sb.AppendLine("        document.getElementById('hud-title').innerText = targetNode.dataset.label;");
        sb.AppendLine("        document.getElementById('hud-body').innerText = 'Category: ' + targetNode.dataset.cat;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    function clearFocus() {");
        sb.AppendLine("      document.querySelectorAll('.node').forEach(n => n.classList.remove('active', 'dimmed'));");
        sb.AppendLine("      document.querySelectorAll('.edge').forEach(e => e.classList.remove('dimmed'));");
        sb.AppendLine("      document.getElementById('hud-title').innerText = 'System Overview';");
        sb.AppendLine($"      document.getElementById('hud-body').innerText = '{doc.Components.Count} components analyzed.';");
        sb.AppendLine("    }");
        sb.AppendLine("    function resetCamera() {");
        sb.AppendLine("      document.getElementById('canvas').setAttribute('viewBox', '0 0 1200 800');");
        sb.AppendLine("      clearFocus();");
        sb.AppendLine("    }");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string CategorizeNode(string name, string type, string? path)
    {
        var text = $"{name} {type} {path ?? ""}".ToLowerInvariant();
        if (text.Contains("controller") || text.Contains("endpoint") || text.Contains("web") || text.Contains("ui") || text.Contains("frontend") || text.Contains("client"))
        {
            return "ui";
        }
        if ((text.Contains("guard") || text.Contains("strategy") || text.Contains("policy") || text.Contains("gate") || text.Contains("role")) && !text.Contains("service"))
        {
            return "policy";
        }
        if (text.Contains("db") || text.Contains("database") || text.Contains("context") || text.Contains("repository") || text.Contains("entity") || text.Contains("prisma"))
        {
            return "data";
        }
        if (text.Contains("external") || text.Contains("oauth") || text.Contains("google") || text.Contains("resend") || text.Contains("smtp") || text.Contains("redis") || text.Contains("kafka"))
        {
            return "external";
        }
        return "runtime";
    }

    private static string CategoryToIcon(string category)
    {
        return category switch
        {
            "ui" => "window",
            "runtime" => "code",
            "policy" => "shield",
            "data" => "db",
            "external" => "cloud",
            _ => "grid"
        };
    }

    private static string InferTag(string name, string type)
    {
        if (name.EndsWith(".Api", StringComparison.OrdinalIgnoreCase)) return "WEBAPI";
        if (name.EndsWith(".Application", StringComparison.OrdinalIgnoreCase)) return "APPLICATION";
        if (name.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase)) return "DOMAIN";
        if (name.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase)) return "INFRA";
        if (type.Contains("Controller", StringComparison.OrdinalIgnoreCase)) return "API";
        if (type.Contains("Service", StringComparison.OrdinalIgnoreCase)) return "SERVICE";
        return "MODULE";
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

