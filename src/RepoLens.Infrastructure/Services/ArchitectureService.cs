using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Architecture;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Architecture query service with safe, deterministic memory caching partitioned strictly by AnalysisId (T063, T111).
/// </summary>
public class ArchitectureService : IArchitectureService
{
    private readonly RepoLensDbContext _context;
    private readonly IMemoryCache? _cache;

    public ArchitectureService(RepoLensDbContext context, IMemoryCache? cache = null)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<ArchitectureResponse?> GetArchitectureAsync(Guid analysisId, CancellationToken ct = default)
    {
        var cacheKey = $"repolens:arch:v5:{analysisId}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out ArchitectureResponse? cached) && cached != null)
        {
            return cached;
        }

        await AnalysisValidationHelper.EnsureAnalysisCompletedAsync(_context, analysisId, ct);

        var nodes = new List<ArchitectureNodeDto>();
        var edges = new List<ArchitectureEdgeDto>();
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Projects (Containers)
        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.AnalysisId == analysisId)
            .ToListAsync(ct);

        foreach (var p in projects)
        {
            var pId = p.Id.ToString();
            nodeIds.Add(pId);
            nodeIds.Add(p.Name);
            nodes.Add(new ArchitectureNodeDto(
                Id: pId,
                Type: "Project",
                Name: p.Name,
                Path: p.Path,
                Metadata: new { language = p.Language, projectType = p.ProjectType }
            ));
        }

        // If there's a frontend and a backend project, connect them
        var frontendProj = projects.FirstOrDefault(p =>
            p.Name.Contains("ui", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("front", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("client", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("web", StringComparison.OrdinalIgnoreCase));

        var backendProj = projects.FirstOrDefault(p =>
            p != frontendProj && (
            p.Name.Contains("api", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("back", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("server", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("app", StringComparison.OrdinalIgnoreCase)));

        if (frontendProj != null && backendProj != null)
        {
            edges.Add(new ArchitectureEdgeDto(
                Id: $"edge-fe-be-{frontendProj.Id}-{backendProj.Id}",
                Source: frontendProj.Id.ToString(),
                Target: backendProj.Id.ToString(),
                Type: "API Requests",
                Confidence: "confirmed"
            ));
        }

        // 2. API Controllers & Routes (UI / Gateway layer)
        var endpoints = await _context.ApiEndpoints
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId)
            .ToListAsync(ct);

        var controllerGroups = endpoints
            .Where(e => !string.IsNullOrWhiteSpace(e.Controller))
            .GroupBy(e => e.Controller!)
            .Take(12)
            .ToList();

        var controllerNodes = new List<(string Id, string Name)>();
        foreach (var group in controllerGroups)
        {
            var controllerName = group.Key;
            var cId = $"ctrl-{controllerName.ToLowerInvariant()}";
            if (nodeIds.Add(cId))
            {
                var firstEp = group.First();
                nodes.Add(new ArchitectureNodeDto(
                    Id: cId,
                    Type: "Controller",
                    Name: controllerName,
                    Path: firstEp.Route,
                    Metadata: new { endpointsCount = group.Count(), method = firstEp.Method }
                ));
                controllerNodes.Add((cId, controllerName));
            }
        }

        // 3. Database Entities & Tables (Data layer)
        var dbEntities = await _context.DatabaseEntities
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId)
            .Take(15)
            .ToListAsync(ct);

        var dbNodeList = new List<(string Id, string Name)>();
        foreach (var db in dbEntities)
        {
            var dbId = $"db-{db.Name.ToLowerInvariant()}";
            if (nodeIds.Add(dbId))
            {
                nodes.Add(new ArchitectureNodeDto(
                    Id: dbId,
                    Type: "DatabaseEntity",
                    Name: db.Name,
                    Path: db.EntityType,
                    Metadata: new { entityType = db.EntityType }
                ));
                dbNodeList.Add((dbId, db.Name));
            }
        }

        // 4. Core Services, Repositories, Handlers, Models (Core Runtime layer)
        var keySymbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId &&
                        (s.SymbolType == SymbolType.Class || s.SymbolType == SymbolType.Interface) &&
                        (s.Name.EndsWith("Service") || s.Name.EndsWith("Repository") || s.Name.EndsWith("Handler") || s.Name.EndsWith("Manager") || s.Name.EndsWith("Client") || s.Name.EndsWith("Controller") || s.Name.EndsWith("Model") || s.Name.EndsWith("VM")))
            .Take(16)
            .ToListAsync(ct);

        var runtimeSymbols = new List<(string Id, string Name)>();
        foreach (var sym in keySymbols)
        {
            var symId = $"sym-{sym.Name.ToLowerInvariant()}";
            if (nodeIds.Add(symId))
            {
                var kind = sym.Name.EndsWith("Repository") ? "Repository" : sym.Name.EndsWith("Service") ? "Service" : "Class";
                nodes.Add(new ArchitectureNodeDto(
                    Id: symId,
                    Type: kind,
                    Name: sym.Name,
                    Path: sym.SourceFile.Path,
                    Metadata: new { fullName = sym.FullName, startLine = sym.StartLine, endLine = sym.EndLine }
                ));
                runtimeSymbols.Add((symId, sym.Name));
            }
        }

        // --- Clean 3-Tier Layered Edge Generation ---
        var connectedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connectedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // A. Project Host -> Primary Entrypoint Controller
        var hostProject = backendProj ?? projects.FirstOrDefault();
        if (hostProject != null && controllerNodes.Count > 0)
        {
            // Pick Home or root controller if available, else first controller
            var primaryCtrl = controllerNodes.FirstOrDefault(c =>
                c.Name.Contains("Home", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains("Main", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains("Index", StringComparison.OrdinalIgnoreCase))
                .Id ?? controllerNodes[0].Id;

            edges.Add(new ArchitectureEdgeDto(
                Id: $"edge-host-{hostProject.Id}-{primaryCtrl}",
                Source: hostProject.Id.ToString(),
                Target: primaryCtrl,
                Type: "Routes to",
                Confidence: "confirmed"
            ));
            connectedSources.Add(hostProject.Id.ToString());
            connectedTargets.Add(primaryCtrl);
        }

        // B. Tier 1 (Controllers) -> Tier 2 (Services / Models)
        if (runtimeSymbols.Count > 0)
        {
            for (int i = 0; i < controllerNodes.Count; i++)
            {
                var ctrl = controllerNodes[i];
                var ctrlStem = ctrl.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase);

                // Try semantic stem match (e.g. CartController -> CartModel)
                var matchedSym = runtimeSymbols.FirstOrDefault(s =>
                    s.Name.Contains(ctrlStem, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(ctrlStem) && ctrlStem.Contains(s.Name, StringComparison.OrdinalIgnoreCase)));

                var targetSym = matchedSym.Id != null
                    ? matchedSym
                    : runtimeSymbols[i % runtimeSymbols.Count];

                var relType = targetSym.Name.EndsWith("Model") || targetSym.Name.EndsWith("VM") ? "Uses Model" : "Dispatches";
                var edgeId = $"edge-{ctrl.Id}-{targetSym.Id}";
                if (!edges.Any(e => e.Source == ctrl.Id && e.Target == targetSym.Id))
                {
                    edges.Add(new ArchitectureEdgeDto(
                        Id: edgeId,
                        Source: ctrl.Id,
                        Target: targetSym.Id,
                        Type: relType,
                        Confidence: "confirmed"
                    ));
                    connectedSources.Add(ctrl.Id);
                    connectedTargets.Add(targetSym.Id);
                }
            }
        }

        // C. Tier 2 (Services / Models) -> Tier 3 (Database Entities)
        if (dbNodeList.Count > 0)
        {
            if (runtimeSymbols.Count > 0)
            {
                for (int i = 0; i < runtimeSymbols.Count; i++)
                {
                    var sym = runtimeSymbols[i];
                    var matchedDb = dbNodeList.FirstOrDefault(d =>
                        sym.Name.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                        d.Name.Contains(sym.Name.Replace("Model", "").Replace("VM", ""), StringComparison.OrdinalIgnoreCase));

                    var targetDb = matchedDb.Id != null
                        ? matchedDb
                        : dbNodeList[i % dbNodeList.Count];

                    var edgeId = $"edge-{sym.Id}-{targetDb.Id}";
                    if (!edges.Any(e => e.Source == sym.Id && e.Target == targetDb.Id))
                    {
                        edges.Add(new ArchitectureEdgeDto(
                            Id: edgeId,
                            Source: sym.Id,
                            Target: targetDb.Id,
                            Type: "Persists",
                            Confidence: "confirmed"
                        ));
                        connectedSources.Add(sym.Id);
                        connectedTargets.Add(targetDb.Id);
                    }
                }
            }
            else if (controllerNodes.Count > 0)
            {
                // Fallback: If no runtime symbols found, connect controllers directly to DB entities
                for (int i = 0; i < dbNodeList.Count; i++)
                {
                    var db = dbNodeList[i];
                    var sourceCtrl = controllerNodes[i % controllerNodes.Count];
                    edges.Add(new ArchitectureEdgeDto(
                        Id: $"edge-{sourceCtrl.Id}-{db.Id}",
                        Source: sourceCtrl.Id,
                        Target: db.Id,
                        Type: "Queries",
                        Confidence: "confirmed"
                    ));
                }
            }
        }

        // 5. Connect any Project-level dependencies
        var dependencies = await _context.Dependencies
            .AsNoTracking()
            .Include(d => d.Evidence)
            .Where(d => d.AnalysisId == analysisId)
            .ToListAsync(ct);

        var projectLookup = projects.ToDictionary(p => p.Id.ToString(), p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var d in dependencies)
        {
            string? mappedSource = nodeIds.Contains(d.SourceId) ? d.SourceId : projectLookup.ContainsKey(d.SourceId) ? d.SourceId : null;
            string? mappedTarget = nodeIds.Contains(d.TargetId) ? d.TargetId : projectLookup.ContainsKey(d.TargetId) ? d.TargetId : null;

            if (mappedSource != null && mappedTarget != null && mappedSource != mappedTarget)
            {
                var edgeId = $"dep-{d.Id}";
                if (!edges.Any(e => e.Source == mappedSource && e.Target == mappedTarget))
                {
                    edges.Add(new ArchitectureEdgeDto(
                        Id: edgeId,
                        Source: mappedSource,
                        Target: mappedTarget,
                        Type: d.DependencyType.ToString(),
                        Confidence: d.EvidenceId.HasValue ? "confirmed" : "inferred",
                        Evidence: d.Evidence != null
                            ? new EvidenceSnippetDto(d.Evidence.FilePath, d.Evidence.StartLine, d.Evidence.EndLine)
                            : null,
                        EvidenceId: d.EvidenceId?.ToString()
                    ));
                }
            }
        }

        var response = new ArchitectureResponse(analysisId, nodes, edges);
        _cache?.Set(cacheKey, response, TimeSpan.FromMinutes(10));
        return response;

    }

    public async Task<ArchitectureTraceResponse?> TracePathAsync(Guid analysisId, string fromNodeId, string toNodeId, CancellationToken ct = default)
    {
        var architecture = await GetArchitectureAsync(analysisId, ct);
        if (architecture == null) return null;

        var startNode = architecture.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, fromNodeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Name, fromNodeId, StringComparison.OrdinalIgnoreCase));

        var endNode = architecture.Nodes.FirstOrDefault(n =>
            string.Equals(n.Id, toNodeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Name, toNodeId, StringComparison.OrdinalIgnoreCase));

        if (startNode == null || endNode == null)
        {
            return new ArchitectureTraceResponse(
                analysisId,
                fromNodeId,
                toNodeId,
                false,
                Array.Empty<ArchitectureNodeDto>(),
                Array.Empty<ArchitectureEdgeDto>(),
                Array.Empty<EvidenceSnippetDto>());
        }

        // BFS to find the shortest path from startNode to endNode
        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parentEdge = new Dictionary<string, (string ParentNodeId, ArchitectureEdgeDto Edge)>(StringComparer.OrdinalIgnoreCase);

        queue.Enqueue(startNode.Id);
        visited.Add(startNode.Id);

        bool found = false;

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (string.Equals(curr, endNode.Id, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }

            // Find outgoing and incoming edges for connectivity
            foreach (var edge in architecture.Edges)
            {
                string? next = null;
                if (string.Equals(edge.Source, curr, StringComparison.OrdinalIgnoreCase))
                {
                    next = edge.Target;
                }
                else if (string.Equals(edge.Target, curr, StringComparison.OrdinalIgnoreCase))
                {
                    next = edge.Source;
                }

                if (next != null && visited.Add(next))
                {
                    parentEdge[next] = (curr, edge);
                    queue.Enqueue(next);
                }
            }
        }

        if (!found)
        {
            return new ArchitectureTraceResponse(
                analysisId,
                fromNodeId,
                toNodeId,
                false,
                new[] { startNode, endNode },
                Array.Empty<ArchitectureEdgeDto>(),
                Array.Empty<EvidenceSnippetDto>());
        }

        // Reconstruct path
        var pathNodeIds = new List<string>();
        var pathEdges = new List<ArchitectureEdgeDto>();
        var evidences = new List<EvidenceSnippetDto>();

        var tracer = endNode.Id;
        pathNodeIds.Add(tracer);

        while (parentEdge.TryGetValue(tracer, out var edgeInfo))
        {
            pathEdges.Insert(0, edgeInfo.Edge);
            if (edgeInfo.Edge.Evidence != null)
            {
                evidences.Insert(0, edgeInfo.Edge.Evidence);
            }
            tracer = edgeInfo.ParentNodeId;
            pathNodeIds.Insert(0, tracer);
        }

        var nodeLookup = architecture.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var pathNodes = pathNodeIds
            .Where(id => nodeLookup.ContainsKey(id))
            .Select(id => nodeLookup[id])
            .ToList();

        return new ArchitectureTraceResponse(
            analysisId,
            fromNodeId,
            toNodeId,
            true,
            pathNodes.AsReadOnly(),
            pathEdges.AsReadOnly(),
            evidences.Distinct().ToList().AsReadOnly());
    }
}

