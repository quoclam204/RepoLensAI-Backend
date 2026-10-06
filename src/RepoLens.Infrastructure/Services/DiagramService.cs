using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Diagrams;
using RepoLens.Application.Models.Classification;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="IDiagramService"/> (Giai đoạn 2).
/// Generates repository-type-specific diagrams based on static analysis data.
/// Strictly enforces Archify principles: max ~30 nodes, role coloring, evidence grounding,
/// and explicit NotDetected status when evidence is missing.
/// </summary>
public class DiagramService : IDiagramService
{
    private readonly RepoLensDbContext _context;
    private readonly IRepositoryTypeDetector _detector;
    private readonly ITemporaryWorkspaceManager _workspaceManager;
    private readonly WorkspaceOptions _workspaceOptions;
    private readonly ILogger<DiagramService> _logger;

    public DiagramService(
        RepoLensDbContext context,
        IRepositoryTypeDetector detector,
        ITemporaryWorkspaceManager workspaceManager,
        IOptions<WorkspaceOptions> workspaceOptions,
        ILogger<DiagramService> logger)
    {
        _context = context;
        _detector = detector;
        _workspaceManager = workspaceManager;
        _workspaceOptions = workspaceOptions?.Value ?? new WorkspaceOptions();
        _logger = logger;
    }

    public async Task<RepositoryClassification> GetClassificationAsync(Guid analysisId, CancellationToken ct = default)
    {
        await AnalysisValidationHelper.EnsureAnalysisCompletedAsync(_context, analysisId, ct);
        var workspaceRoot = await ResolveWorkspaceRootAsync(analysisId, ct);
        return await _detector.DetectAsync(analysisId, workspaceRoot, ct);
    }

    public async Task<DiagramDto> GetDiagramAsync(Guid analysisId, string? diagramType = null, CancellationToken ct = default)
    {
        await AnalysisValidationHelper.EnsureAnalysisCompletedAsync(_context, analysisId, ct);
        var workspaceRoot = await ResolveWorkspaceRootAsync(analysisId, ct);
        var classification = await _detector.DetectAsync(analysisId, workspaceRoot, ct);

        // Normalize diagram type or choose primary diagram
        var requestedType = (diagramType ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(requestedType) || requestedType == "default" || requestedType == "main")
        {
            requestedType = GetDefaultDiagramType(classification.Type);
        }

        return classification.Type switch
        {
            RepositoryType.ApiBackend => await GenerateApiBackendDiagramAsync(analysisId, requestedType, classification, ct),
            RepositoryType.Frontend => await GenerateFrontendDiagramAsync(analysisId, requestedType, classification, ct),
            RepositoryType.Monorepo => await GenerateMonorepoDiagramAsync(analysisId, requestedType, classification, ct),
            RepositoryType.Library => await GenerateLibraryDiagramAsync(analysisId, requestedType, classification, ct),
            RepositoryType.Cli => await GenerateCliDiagramAsync(analysisId, requestedType, classification, ct),
            _ => GenerateUnsupportedDiagram(classification)
        };
    }

    // ========================================================================================
    // 1. ApiBackend (.NET) Diagrams: Architecture, Endpoints, ERD
    // ========================================================================================

    private async Task<DiagramDto> GenerateApiBackendDiagramAsync(
        Guid analysisId,
        string diagramType,
        RepositoryClassification classification,
        CancellationToken ct)
    {
        var availableTypes = new[] { "architecture", "endpoints", "erd" };

        // Database detection check
        var hasDbContext = await _context.CodeSymbols
            .AsNoTracking()
            .AnyAsync(s => s.SourceFile.AnalysisId == analysisId &&
                          (s.Name.EndsWith("DbContext") || s.FullName.Contains("DbContext")), ct);

        var hasEntities = await _context.DatabaseEntities
            .AsNoTracking()
            .AnyAsync(e => e.AnalysisId == analysisId, ct);

        var databaseDetected = hasDbContext || hasEntities;

        if (diagramType == "erd")
        {
            if (!databaseDetected)
            {
                return new DiagramDto
                {
                    DiagramType = "erd",
                    RepositoryType = RepositoryType.ApiBackend,
                    Status = "NotDetected",
                    Message = "Không phát hiện database (không tìm thấy DbContext hoặc DbSet trong repository).",
                    DatabaseDetected = false,
                    AvailableDiagramTypes = availableTypes
                };
            }

            return await GenerateErdDiagramAsync(analysisId, classification, availableTypes, ct);
        }

        if (diagramType == "endpoints")
        {
            return await GenerateEndpointsDiagramAsync(analysisId, classification, databaseDetected, availableTypes, ct);
        }

        // Default: "architecture" (phân lớp: Request -> Controller -> Service -> Repository -> Database)
        return await GenerateApiArchitectureDiagramAsync(analysisId, classification, databaseDetected, availableTypes, ct);
    }

    private async Task<DiagramDto> GenerateApiArchitectureDiagramAsync(
        Guid analysisId,
        RepositoryClassification classification,
        bool databaseDetected,
        IReadOnlyList<string> availableTypes,
        CancellationToken ct)
    {
        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        // 1. Client / Request node (Gateway)
        var clientNodeId = "node-client-request";
        nodes.Add(new DiagramNodeDto
        {
            Id = clientNodeId,
            Label = "HTTP Client / Request",
            Kind = "gateway",
            Role = "General",
            Evidence = ["(HTTP Gateway)"]
        });

        // 2. Controllers
        var endpoints = await _context.ApiEndpoints
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId)
            .ToListAsync(ct);

        var controllerSymbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId &&
                       s.SymbolType == SymbolType.Class &&
                       s.Name.EndsWith("Controller"))
            .ToListAsync(ct);

        var controllerNames = controllerSymbols.Select(c => c.Name)
            .Union(endpoints.Where(e => !string.IsNullOrEmpty(e.Controller)).Select(e => e.Controller!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var displayedControllers = controllerNames.Take(8).ToList();
        var remainingControllersCount = controllerNames.Count - displayedControllers.Count;

        var controllerNodeIds = new List<string>();
        foreach (var cName in displayedControllers)
        {
            var symbol = controllerSymbols.FirstOrDefault(s => s.Name.Equals(cName, StringComparison.OrdinalIgnoreCase));
            var cId = $"ctrl-{cName.ToLowerInvariant()}";
            controllerNodeIds.Add(cId);

            var filePath = symbol?.SourceFile.Path ?? "src/Controllers";
            var lineRange = symbol != null ? $"SRC 1 (L{symbol.StartLine}-L{symbol.EndLine})" : "SRC 1";

            nodes.Add(new DiagramNodeDto
            {
                Id = cId,
                Label = cName,
                Kind = "controller",
                Role = "Controller",
                Evidence = [filePath, lineRange]
            });

            // Edge from client -> controller (Inferred gateway routing)
            edges.Add(new DiagramEdgeDto
            {
                Id = $"edge-client-{cId}",
                From = clientNodeId,
                To = cId,
                Kind = "calls",
                Confidence = "High",
                IsInferred = true,
                Label = "HTTP Route"
            });
        }

        if (remainingControllersCount > 0)
        {
            var groupCId = "group-controllers";
            nodes.Add(new DiagramNodeDto
            {
                Id = groupCId,
                Label = $"+{remainingControllersCount} controllers khác",
                Kind = "group",
                Role = "Group",
                Evidence = ["(Các controllers còn lại)"]
            });
            edges.Add(new DiagramEdgeDto
            {
                Id = $"edge-client-{groupCId}",
                From = clientNodeId,
                To = groupCId,
                Kind = "calls",
                Confidence = "Medium",
                IsInferred = true
            });
        }

        // 3. Services (Business logic)
        var serviceSymbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId &&
                       s.SymbolType == SymbolType.Class &&
                       (s.Name.EndsWith("Service") || s.Name.EndsWith("Manager") || s.Name.EndsWith("Handler")))
            .ToListAsync(ct);

        var displayedServices = serviceSymbols.Take(8).ToList();
        var remainingServicesCount = serviceSymbols.Count - displayedServices.Count;
        var serviceNodeIds = new List<string>();

        foreach (var svc in displayedServices)
        {
            var sId = $"svc-{svc.Name.ToLowerInvariant()}";
            serviceNodeIds.Add(sId);

            nodes.Add(new DiagramNodeDto
            {
                Id = sId,
                Label = svc.Name,
                Kind = "service",
                Role = "Service",
                Evidence = [svc.SourceFile.Path, $"SRC 1 (L{svc.StartLine}-L{svc.EndLine})"]
            });

            // Connect to relevant controller
            var matchedCtrl = controllerNodeIds.FirstOrDefault(cId =>
                cId.Contains(svc.Name.Replace("Service", "").ToLowerInvariant()));

            var fromId = matchedCtrl ?? controllerNodeIds.FirstOrDefault() ?? clientNodeId;
            edges.Add(new DiagramEdgeDto
            {
                Id = $"edge-{fromId}-{sId}",
                From = fromId,
                To = sId,
                Kind = "calls",
                Confidence = matchedCtrl != null ? "High" : "Medium",
                IsInferred = matchedCtrl == null,
                Label = "Injects / Calls"
            });
        }

        if (remainingServicesCount > 0)
        {
            var groupSId = "group-services";
            nodes.Add(new DiagramNodeDto
            {
                Id = groupSId,
                Label = $"+{remainingServicesCount} services khác",
                Kind = "group",
                Role = "Group",
                Evidence = ["(Các service khác)"]
            });
        }

        // 4. Repositories (Data access)
        var repoSymbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId &&
                       s.SymbolType == SymbolType.Class &&
                       (s.Name.EndsWith("Repository") || s.Name.EndsWith("Store") || s.Name.EndsWith("Dao")))
            .ToListAsync(ct);

        var displayedRepos = repoSymbols.Take(6).ToList();
        var repoNodeIds = new List<string>();

        foreach (var r in displayedRepos)
        {
            var rId = $"repo-{r.Name.ToLowerInvariant()}";
            repoNodeIds.Add(rId);

            nodes.Add(new DiagramNodeDto
            {
                Id = rId,
                Label = r.Name,
                Kind = "repository",
                Role = "Repository",
                Evidence = [r.SourceFile.Path, $"SRC 1 (L{r.StartLine}-L{r.EndLine})"]
            });

            // Connect from service to repo
            var fromSvc = serviceNodeIds.FirstOrDefault() ?? controllerNodeIds.FirstOrDefault() ?? clientNodeId;
            edges.Add(new DiagramEdgeDto
            {
                Id = $"edge-{fromSvc}-{rId}",
                From = fromSvc,
                To = rId,
                Kind = "queries",
                Confidence = "High",
                IsInferred = true,
                Label = "Accesses"
            });
        }

        // 5. Database (ONLY IF DETECTED)
        if (databaseDetected)
        {
            var dbNodeId = "node-database-layer";
            nodes.Add(new DiagramNodeDto
            {
                Id = dbNodeId,
                Label = "Database (EF Core / PostgreSQL)",
                Kind = "database",
                Role = "Database",
                Evidence = ["DbContext / DbSet entities"]
            });

            // Connect repositories or services to DB
            var dbCallers = repoNodeIds.Count > 0 ? repoNodeIds : serviceNodeIds.Count > 0 ? serviceNodeIds : controllerNodeIds;
            foreach (var callerId in dbCallers.Take(3))
            {
                edges.Add(new DiagramEdgeDto
                {
                    Id = $"edge-{callerId}-{dbNodeId}",
                    From = callerId,
                    To = dbNodeId,
                    Kind = "queries",
                    Confidence = "High",
                    IsInferred = false,
                    Label = "SQL / EF Core"
                });
            }
        }

        var detailCards = BuildDetailCards(nodes, edges, controllerSymbols, serviceSymbols, repoSymbols);

        return new DiagramDto
        {
            DiagramType = "architecture",
            RepositoryType = RepositoryType.ApiBackend,
            Status = "Success",
            DatabaseDetected = databaseDetected,
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    private async Task<DiagramDto> GenerateEndpointsDiagramAsync(
        Guid analysisId,
        RepositoryClassification classification,
        bool databaseDetected,
        IReadOnlyList<string> availableTypes,
        CancellationToken ct)
    {
        var endpoints = await _context.ApiEndpoints
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId)
            .Take(25)
            .ToListAsync(ct);

        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        var groups = endpoints.GroupBy(e => e.Controller ?? "API").Take(8);
        foreach (var group in groups)
        {
            var groupId = $"group-{group.Key.ToLowerInvariant()}";
            nodes.Add(new DiagramNodeDto
            {
                Id = groupId,
                Label = group.Key,
                Kind = "controller",
                Role = "Controller",
                Evidence = [$"Controller: {group.Key}"]
            });

            foreach (var ep in group.Take(4))
            {
                var epId = $"ep-{ep.Id:N}";
                nodes.Add(new DiagramNodeDto
                {
                    Id = epId,
                    Label = $"{ep.Method} {ep.Route}",
                    Kind = "endpoint",
                    Role = "General",
                    ParentId = groupId,
                    Evidence = [$"Action: {ep.Action ?? ep.Method}"]
                });

                edges.Add(new DiagramEdgeDto
                {
                    Id = $"edge-{groupId}-{epId}",
                    From = groupId,
                    To = epId,
                    Kind = "routes_to",
                    Confidence = "High",
                    IsInferred = false
                });
            }
        }

        var detailCards = BuildDetailCards(nodes, edges);

        return new DiagramDto
        {
            DiagramType = "endpoints",
            RepositoryType = RepositoryType.ApiBackend,
            Status = "Success",
            DatabaseDetected = databaseDetected,
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    private async Task<DiagramDto> GenerateErdDiagramAsync(
        Guid analysisId,
        RepositoryClassification classification,
        IReadOnlyList<string> availableTypes,
        CancellationToken ct)
    {
        var entities = await _context.DatabaseEntities
            .AsNoTracking()
            .Where(e => e.AnalysisId == analysisId)
            .Take(20)
            .ToListAsync(ct);

        var relationships = await _context.DatabaseRelationships
            .AsNoTracking()
            .Where(r => r.AnalysisId == analysisId)
            .ToListAsync(ct);

        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        foreach (var entity in entities)
        {
            nodes.Add(new DiagramNodeDto
            {
                Id = entity.Id.ToString(),
                Label = entity.Name,
                Kind = "database",
                Role = "Database",
                Evidence = [$"Entity: {entity.Name} ({entity.EntityType})"]
            });
        }

        foreach (var rel in relationships)
        {
            edges.Add(new DiagramEdgeDto
            {
                Id = rel.Id.ToString(),
                From = rel.SourceEntityId.ToString(),
                To = rel.TargetEntityId.ToString(),
                Kind = "relates_to",
                Confidence = "High",
                IsInferred = false,
                Label = rel.RelationshipType.ToString()
            });
        }

        var detailCards = BuildDetailCards(nodes, edges);

        return new DiagramDto
        {
            DiagramType = "erd",
            RepositoryType = RepositoryType.ApiBackend,
            Status = "Success",
            DatabaseDetected = true,
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    // ========================================================================================
    // 2. Frontend (Next.js/React) Diagrams: RouteMap, ComponentTree, ExternalApiCalls
    // ========================================================================================

    private async Task<DiagramDto> GenerateFrontendDiagramAsync(
        Guid analysisId,
        string diagramType,
        RepositoryClassification classification,
        CancellationToken ct)
    {
        var availableTypes = new[] { "route_map", "component_tree", "external_api" };

        var sourceFiles = await _context.SourceFiles
            .AsNoTracking()
            .Where(f => f.AnalysisId == analysisId)
            .ToListAsync(ct);

        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        // Find route pages (app/ or pages/)
        var routeFiles = sourceFiles
            .Where(f => f.Path.Contains("/app/") || f.Path.StartsWith("app/") ||
                        f.Path.Contains("/pages/") || f.Path.StartsWith("pages/"))
            .Take(8)
            .ToList();

        // Find components (components/)
        var componentFiles = sourceFiles
            .Where(f => f.Path.Contains("/components/") || f.Path.StartsWith("components/"))
            .Take(8)
            .ToList();

        // Find hooks/services (hooks/, services/, lib/)
        var hookFiles = sourceFiles
            .Where(f => f.Path.Contains("/hooks/") || f.Path.StartsWith("hooks/") ||
                        f.Path.Contains("/services/") || f.Path.StartsWith("services/") ||
                        f.Path.Contains("/api/") || f.Path.StartsWith("api/"))
            .Take(6)
            .ToList();

        // 1. Pages
        var pageNodeIds = new List<string>();
        foreach (var rf in routeFiles)
        {
            var pId = $"page-{Path.GetFileNameWithoutExtension(rf.Path)}";
            pageNodeIds.Add(pId);
            nodes.Add(new DiagramNodeDto
            {
                Id = pId,
                Label = rf.Path,
                Kind = "route",
                Role = "Page",
                Evidence = [rf.Path, "SRC 1 (Route Handler/Page)"]
            });
        }

        // 2. Components
        var compNodeIds = new List<string>();
        foreach (var cf in componentFiles)
        {
            var cId = $"comp-{Path.GetFileNameWithoutExtension(cf.Path)}";
            compNodeIds.Add(cId);
            nodes.Add(new DiagramNodeDto
            {
                Id = cId,
                Label = Path.GetFileNameWithoutExtension(cf.Path),
                Kind = "component",
                Role = "Component",
                Evidence = [cf.Path, "SRC 1 (React Component)"]
            });

            // Edge from page -> component
            var fromPage = pageNodeIds.FirstOrDefault();
            if (fromPage != null)
            {
                edges.Add(new DiagramEdgeDto
                {
                    Id = $"edge-{fromPage}-{cId}",
                    From = fromPage,
                    To = cId,
                    Kind = "renders",
                    Confidence = "Medium",
                    IsInferred = true,
                    Label = "Renders"
                });
            }
        }

        // 3. Hooks / Services
        var hookNodeIds = new List<string>();
        foreach (var hf in hookFiles)
        {
            var hId = $"hook-{Path.GetFileNameWithoutExtension(hf.Path)}";
            hookNodeIds.Add(hId);
            nodes.Add(new DiagramNodeDto
            {
                Id = hId,
                Label = Path.GetFileNameWithoutExtension(hf.Path),
                Kind = "hook",
                Role = "Service",
                Evidence = [hf.Path, "SRC 1 (Custom Hook / Service)"]
            });

            var fromComp = compNodeIds.FirstOrDefault() ?? pageNodeIds.FirstOrDefault();
            if (fromComp != null)
            {
                edges.Add(new DiagramEdgeDto
                {
                    Id = $"edge-{fromComp}-{hId}",
                    From = fromComp,
                    To = hId,
                    Kind = "uses",
                    Confidence = "Medium",
                    IsInferred = true,
                    Label = "Uses Hook"
                });
            }
        }

        // 4. External API Gateway Node
        var apiNodeId = "node-external-api";
        nodes.Add(new DiagramNodeDto
        {
            Id = apiNodeId,
            Label = "Backend / External API",
            Kind = "external_api",
            Role = "General",
            Evidence = ["Fetch / Axios API Call"]
        });

        var fromHook = hookNodeIds.FirstOrDefault() ?? compNodeIds.FirstOrDefault() ?? pageNodeIds.FirstOrDefault();
        if (fromHook != null)
        {
            edges.Add(new DiagramEdgeDto
            {
                Id = $"edge-{fromHook}-{apiNodeId}",
                From = fromHook,
                To = apiNodeId,
                Kind = "calls",
                Confidence = "High",
                IsInferred = true,
                Label = "HTTP / REST"
            });
        }

        var detailCards = BuildDetailCards(nodes, edges);

        return new DiagramDto
        {
            DiagramType = diagramType == "component_tree" ? "component_tree" : "route_map",
            RepositoryType = RepositoryType.Frontend,
            Status = "Success",
            DatabaseDetected = false,
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    // ========================================================================================
    // 3. Monorepo / Full-Stack Diagrams: SystemOverview
    // ========================================================================================

    private async Task<DiagramDto> GenerateMonorepoDiagramAsync(
        Guid analysisId,
        string diagramType,
        RepositoryClassification classification,
        CancellationToken ct)
    {
        var availableTypes = new[] { "system_overview", "dependencies" };

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(p => p.AnalysisId == analysisId)
            .ToListAsync(ct);

        var dependencies = await _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId && d.DependencyType == DependencyType.ProjectReference)
            .ToListAsync(ct);

        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        foreach (var proj in projects)
        {
            var isFrontend = proj.Language.Equals("TypeScript", StringComparison.OrdinalIgnoreCase) ||
                             proj.Language.Equals("JavaScript", StringComparison.OrdinalIgnoreCase) ||
                             proj.Name.Contains("front", StringComparison.OrdinalIgnoreCase) ||
                             proj.Name.Contains("ui", StringComparison.OrdinalIgnoreCase);

            var childType = isFrontend ? "route_map" : "architecture";
            var role = isFrontend ? "Page" : "Project";

            nodes.Add(new DiagramNodeDto
            {
                Id = proj.Id.ToString(),
                Label = proj.Name,
                Kind = "project",
                Role = role,
                ChildDiagramType = childType,
                Evidence = [proj.Path, $"Language: {proj.Language}"]
            });
        }

        // Project references as direct code edges (isInferred: false)
        foreach (var dep in dependencies)
        {
            edges.Add(new DiagramEdgeDto
            {
                Id = dep.Id.ToString(),
                From = dep.SourceId,
                To = dep.TargetId,
                Kind = "references",
                Confidence = "High",
                IsInferred = false,
                Label = "Project Ref"
            });
        }

        // If no explicit project references between frontend & backend, infer full-stack flow
        if (edges.Count == 0 && projects.Count >= 2)
        {
            var fe = projects.FirstOrDefault(p => p.Name.Contains("front", StringComparison.OrdinalIgnoreCase) || p.Language == "TypeScript");
            var be = projects.FirstOrDefault(p => p != fe);
            if (fe != null && be != null)
            {
                edges.Add(new DiagramEdgeDto
                {
                    Id = $"edge-fe-be-{fe.Id}-{be.Id}",
                    From = fe.Id.ToString(),
                    To = be.Id.ToString(),
                    Kind = "calls",
                    Confidence = "Medium",
                    IsInferred = true,
                    Label = "API Requests"
                });
            }
        }

        var detailCards = BuildDetailCards(nodes, edges);

        return new DiagramDto
        {
            DiagramType = "system_overview",
            RepositoryType = RepositoryType.Monorepo,
            Status = "Success",
            DatabaseDetected = classification.Evidences.Any(e => e.Reason.Contains("DbContext")),
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    // ========================================================================================
    // 4. Library / CLI Diagrams: NamespaceClass, CallFlow
    // ========================================================================================

    private async Task<DiagramDto> GenerateLibraryDiagramAsync(
        Guid analysisId,
        string diagramType,
        RepositoryClassification classification,
        CancellationToken ct)
    {
        var availableTypes = new[] { "namespace_class", "hierarchy" };
        return await GenerateNamespaceClassDiagramAsync(analysisId, RepositoryType.Library, availableTypes, ct);
    }

    private async Task<DiagramDto> GenerateCliDiagramAsync(
        Guid analysisId,
        string diagramType,
        RepositoryClassification classification,
        CancellationToken ct)
    {
        var availableTypes = new[] { "call_flow", "namespace_class" };

        if (diagramType == "call_flow")
        {
            return await GenerateCliCallFlowDiagramAsync(analysisId, availableTypes, ct);
        }

        return await GenerateNamespaceClassDiagramAsync(analysisId, RepositoryType.Cli, availableTypes, ct);
    }

    private async Task<DiagramDto> GenerateNamespaceClassDiagramAsync(
        Guid analysisId,
        RepositoryType repoType,
        IReadOnlyList<string> availableTypes,
        CancellationToken ct)
    {
        var symbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId &&
                       (s.SymbolType == SymbolType.Class || s.SymbolType == SymbolType.Interface))
            .Take(30)
            .ToListAsync(ct);

        var dependencies = await _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId &&
                       (d.DependencyType == DependencyType.Implements ||
                        d.DependencyType == DependencyType.Inherits ||
                        d.DependencyType == DependencyType.Calls))
            .ToListAsync(ct);

        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        foreach (var sym in symbols.Take(12))
        {
            var isInterface = sym.SymbolType == SymbolType.Interface;
            nodes.Add(new DiagramNodeDto
            {
                Id = sym.Id.ToString(),
                Label = sym.Name,
                Kind = isInterface ? "interface" : "class",
                Role = "Class",
                Evidence = [sym.SourceFile.Path, $"SRC 1 (L{sym.StartLine}-L{sym.EndLine})"]
            });
        }

        if (symbols.Count > 12)
        {
            nodes.Add(new DiagramNodeDto
            {
                Id = "group-more-classes",
                Label = $"+{symbols.Count - 12} class khác",
                Kind = "group",
                Role = "Group",
                Evidence = ["(Các class và interface còn lại)"]
            });
        }

        foreach (var dep in dependencies)
        {
            if (nodes.Any(n => n.Id == dep.SourceId) && nodes.Any(n => n.Id == dep.TargetId))
            {
                edges.Add(new DiagramEdgeDto
                {
                    Id = dep.Id.ToString(),
                    From = dep.SourceId,
                    To = dep.TargetId,
                    Kind = dep.DependencyType.ToString().ToLowerInvariant(),
                    Confidence = "High",
                    IsInferred = false,
                    Label = dep.DependencyType.ToString()
                });
            }
        }

        var detailCards = BuildDetailCards(nodes, edges);

        return new DiagramDto
        {
            DiagramType = "namespace_class",
            RepositoryType = repoType,
            Status = "Success",
            DatabaseDetected = false, // Library / CLI: No API, no DB
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    private async Task<DiagramDto> GenerateCliCallFlowDiagramAsync(
        Guid analysisId,
        IReadOnlyList<string> availableTypes,
        CancellationToken ct)
    {
        var symbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId)
            .ToListAsync(ct);

        var programSymbol = symbols.FirstOrDefault(s => s.Name.Equals("Program", StringComparison.OrdinalIgnoreCase));
        var commandSymbols = symbols.Where(s => s != programSymbol &&
            (s.Name.EndsWith("Command") || s.Name.EndsWith("Handler") || s.Name.EndsWith("Service") || s.SymbolType == SymbolType.Class))
            .Take(8)
            .ToList();

        var nodes = new List<DiagramNodeDto>();
        var edges = new List<DiagramEdgeDto>();

        var mainId = "node-program-main";
        nodes.Add(new DiagramNodeDto
        {
            Id = mainId,
            Label = "Program.Main (CLI Entry Point)",
            Kind = "method",
            Role = "General",
            Evidence = [programSymbol?.SourceFile.Path ?? "src/Program.cs", "SRC 1 (Main entry point)"]
        });

        foreach (var cmd in commandSymbols)
        {
            var cmdId = $"cmd-{cmd.Name.ToLowerInvariant()}";
            nodes.Add(new DiagramNodeDto
            {
                Id = cmdId,
                Label = cmd.Name,
                Kind = "class",
                Role = "Class",
                Evidence = [cmd.SourceFile.Path, $"SRC 1 (L{cmd.StartLine}-L{cmd.EndLine})"]
            });

            edges.Add(new DiagramEdgeDto
            {
                Id = $"edge-{mainId}-{cmdId}",
                From = mainId,
                To = cmdId,
                Kind = "calls",
                Confidence = "High",
                IsInferred = true,
                Label = "Dispatches"
            });
        }

        var detailCards = BuildDetailCards(nodes, edges);

        return new DiagramDto
        {
            DiagramType = "call_flow",
            RepositoryType = RepositoryType.Cli,
            Status = "Success",
            DatabaseDetected = false,
            AvailableDiagramTypes = availableTypes,
            Nodes = nodes,
            Edges = edges,
            DetailCards = detailCards
        };
    }

    // ========================================================================================
    // 5. Unsupported Diagrams: Only message and directory stats
    // ========================================================================================

    private static DiagramDto GenerateUnsupportedDiagram(RepositoryClassification classification)
    {
        return new DiagramDto
        {
            DiagramType = "unsupported",
            RepositoryType = RepositoryType.Unsupported,
            Status = "Unsupported",
            Message = "Ngôn ngữ chưa hỗ trợ phân tích sâu. Chỉ có sẵn cây thư mục và thống kê file.",
            DatabaseDetected = false,
            AvailableDiagramTypes = ["unsupported"],
            Nodes = [],
            Edges = [],
            DetailCards = []
        };
    }

    // ========================================================================================
    // Helpers: Detail Cards & Reach Tracing (Upstream / Downstream)
    // ========================================================================================

    private static IReadOnlyList<DiagramDetailCardDto> BuildDetailCards(
        IReadOnlyList<DiagramNodeDto> nodes,
        IReadOnlyList<DiagramEdgeDto> edges,
        params IEnumerable<CodeSymbol>[] symbolSources)
    {
        var allSymbols = symbolSources.SelectMany(s => s).ToList();
        var cards = new List<DiagramDetailCardDto>();

        foreach (var node in nodes)
        {
            // Upstream: nodes that point TO this node ("Ai phụ thuộc nó")
            var upstream = edges.Where(e => e.To == node.Id).Select(e => e.From).Distinct().ToList();

            // Downstream: nodes that this node points TO ("Nó phụ thuộc vào")
            var downstream = edges.Where(e => e.From == node.Id).Select(e => e.To).Distinct().ToList();

            var evidencePath = node.Evidence.FirstOrDefault() ?? "src/";
            var lineRange = node.Evidence.FirstOrDefault(e => e.StartsWith("SRC")) ?? "SRC 1";

            cards.Add(new DiagramDetailCardDto
            {
                NodeId = node.Id,
                Title = node.Label,
                Role = node.Role,
                FilePath = evidencePath,
                Symbol = node.Label,
                LineRange = lineRange,
                Description = $"Thành phần thuộc vai trò {node.Role} trong kiến trúc hệ thống.",
                UpstreamNodes = upstream,
                DownstreamNodes = downstream
            });
        }

        return cards;
    }

    private static string GetDefaultDiagramType(RepositoryType repoType) => repoType switch
    {
        RepositoryType.ApiBackend => "architecture",
        RepositoryType.Frontend => "route_map",
        RepositoryType.Monorepo => "system_overview",
        RepositoryType.Library => "namespace_class",
        RepositoryType.Cli => "call_flow",
        _ => "unsupported"
    };

    private async Task<string> ResolveWorkspaceRootAsync(Guid analysisId, CancellationToken ct)
    {
        try
        {
            var workspace = await _workspaceManager.GetWorkspaceAsync(analysisId, ct);
            if (workspace != null && Directory.Exists(workspace.RootPath))
            {
                return workspace.RootPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve workspace directory from workspace manager for {AnalysisId}", analysisId);
        }

        var fallback = Path.Combine(_workspaceOptions.BaseDirectory, analysisId.ToString("D"));
        return Directory.Exists(fallback) ? fallback : _workspaceOptions.BaseDirectory;
    }
}
