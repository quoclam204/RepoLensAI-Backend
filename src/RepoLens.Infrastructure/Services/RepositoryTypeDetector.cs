using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Models.Classification;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Detects repository type using a 3-layer evidence approach.
/// Layer 1: File markers (project files, config files) — cheapest, runs first.
/// Layer 2: Code-level patterns (attributes, base classes, entry points) — uses persisted analysis data.
/// Layer 3: Component relationships (project references, dependency graph) — uses persisted dependency data.
///
/// Only reads files and queries DB. Never executes repository code.
/// </summary>
public class RepositoryTypeDetector : IRepositoryTypeDetector
{
    private readonly RepoLensDbContext _context;
    private readonly ILogger<RepositoryTypeDetector> _logger;

    public RepositoryTypeDetector(RepoLensDbContext context, ILogger<RepositoryTypeDetector> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RepositoryClassification> DetectAsync(Guid analysisId, string workspaceRoot, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting repository type detection for analysis {AnalysisId} at {WorkspaceRoot}", analysisId, workspaceRoot);

        var evidences = new List<ClassificationEvidence>();
        var detectedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // === LAYER 1: File Markers ===
        var layer1 = ScanFileMarkers(workspaceRoot, detectedLanguages);
        evidences.AddRange(layer1);

        // === LAYER 2: Code-Level Patterns (from persisted analysis) ===
        var layer2 = await ScanCodePatternsAsync(analysisId, ct);
        evidences.AddRange(layer2);

        // === LAYER 3: Component Relationships ===
        var layer3 = await ScanRelationshipsAsync(analysisId, ct);
        evidences.AddRange(layer3);

        // === Classification Decision ===
        var (repoType, confidence, summary) = Classify(layer1, layer2, layer3, detectedLanguages);

        _logger.LogInformation(
            "Repository type detection complete for {AnalysisId}: Type={Type}, Confidence={Confidence}, Languages=[{Languages}]",
            analysisId, repoType, confidence, string.Join(", ", detectedLanguages));

        return new RepositoryClassification
        {
            Type = repoType,
            DetectedLanguages = detectedLanguages.ToList().AsReadOnly(),
            Confidence = confidence,
            Evidences = evidences.AsReadOnly(),
            Summary = summary
        };
    }

    // ========================================================================================
    // LAYER 1: File Markers
    // ========================================================================================

    private List<ClassificationEvidence> ScanFileMarkers(string workspaceRoot, HashSet<string> detectedLanguages)
    {
        var evidences = new List<ClassificationEvidence>();

        if (!Directory.Exists(workspaceRoot))
        {
            _logger.LogWarning("Workspace root does not exist: {WorkspaceRoot}", workspaceRoot);
            return evidences;
        }

        // Scan root directory
        ScanDirectoryForMarkers(workspaceRoot, workspaceRoot, detectedLanguages, evidences, isRoot: true);

        // Scan immediate subdirectories for monorepo detection
        try
        {
            foreach (var subDir in Directory.GetDirectories(workspaceRoot))
            {
                var dirName = Path.GetFileName(subDir);
                // Skip common non-project directories
                if (IsIgnoredDirectory(dirName)) continue;
                ScanDirectoryForMarkers(subDir, workspaceRoot, detectedLanguages, evidences, isRoot: false);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied scanning subdirectories of {WorkspaceRoot}", workspaceRoot);
        }

        return evidences;
    }

    private void ScanDirectoryForMarkers(
        string directory,
        string workspaceRoot,
        HashSet<string> detectedLanguages,
        List<ClassificationEvidence> evidences,
        bool isRoot)
    {
        try
        {
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
                var ext = Path.GetExtension(fileName).ToLowerInvariant();

                // .NET markers
                if (ext == ".sln")
                {
                    detectedLanguages.Add("C#");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "Solution file (.sln) indicates a .NET project",
                        Layer = 1
                    });
                }
                else if (ext == ".csproj")
                {
                    detectedLanguages.Add("C#");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = $".NET project file found{(isRoot ? " at root" : " in subdirectory")}",
                        Layer = 1
                    });
                }
                // JS/TS markers
                else if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase))
                {
                    AddPackageJsonEvidence(file, relativePath, detectedLanguages, evidences);
                }
                else if (fileName.StartsWith("next.config", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("TypeScript");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "next.config file indicates a Next.js frontend project",
                        Layer = 1
                    });
                }
                else if (fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("TypeScript");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "TypeScript configuration found",
                        Layer = 1
                    });
                }
                // Docker markers
                else if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
                {
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "Dockerfile indicates containerized deployment",
                        Layer = 1
                    });
                }
                else if (fileName.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase))
                {
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "docker-compose indicates multi-service deployment, possible monorepo",
                        Layer = 1
                    });
                }
                // Python markers
                else if (fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("manage.py", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("Pipfile", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("Python");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = $"Python project file ({fileName}) detected (FastAPI / Django / Flask / Web)",
                        Layer = 1
                    });
                }
                // Java markers
                else if (fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("Java");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = $"Java project file ({fileName}) detected (Spring Boot / Maven / Gradle)",
                        Layer = 1
                    });
                }
                // Go markers
                else if (fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("Go");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "Go module file (go.mod) detected (Gin / Fiber / Web)",
                        Layer = 1
                    });
                }
                // PHP markers
                else if (fileName.Equals("composer.json", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("PHP");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "PHP Composer project file detected (Laravel / Symfony / Web)",
                        Layer = 1
                    });
                }
                // Rust markers
                else if (fileName.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))
                {
                    detectedLanguages.Add("Rust");
                    evidences.Add(new ClassificationEvidence
                    {
                        FilePath = relativePath,
                        Reason = "Rust Cargo package file detected (Actix / Axum / Web)",
                        Layer = 1
                    });
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied scanning directory: {Directory}", directory);
        }
    }

    private void AddPackageJsonEvidence(
        string filePath,
        string relativePath,
        HashSet<string> detectedLanguages,
        List<ClassificationEvidence> evidences)
    {
        detectedLanguages.Add("JavaScript");
        evidences.Add(new ClassificationEvidence
        {
            FilePath = relativePath,
            Reason = "package.json found — Node.js/JavaScript project",
            Layer = 1
        });

        // Attempt to read dependencies for framework detection (read-only, no execution)
        try
        {
            var content = File.ReadAllText(filePath);

            if (content.Contains("\"next\"", StringComparison.OrdinalIgnoreCase))
            {
                detectedLanguages.Add("TypeScript");
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = relativePath,
                    Reason = "Next.js dependency detected in package.json",
                    Layer = 1
                });
            }

            if (content.Contains("\"react\"", StringComparison.OrdinalIgnoreCase))
            {
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = relativePath,
                    Reason = "React dependency detected in package.json",
                    Layer = 1
                });
            }

            if (content.Contains("\"express\"", StringComparison.OrdinalIgnoreCase))
            {
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = relativePath,
                    Reason = "Express.js dependency detected — Node.js backend/API",
                    Layer = 1
                });
            }

            if (content.Contains("\"vue\"", StringComparison.OrdinalIgnoreCase))
            {
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = relativePath,
                    Reason = "Vue.js dependency detected — frontend framework",
                    Layer = 1
                });
            }

            if (content.Contains("\"angular\"", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("\"@angular/core\"", StringComparison.OrdinalIgnoreCase))
            {
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = relativePath,
                    Reason = "Angular dependency detected — frontend framework",
                    Layer = 1
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read package.json at {FilePath}", filePath);
        }
    }

    // ========================================================================================
    // LAYER 2: Code-Level Patterns (from persisted analysis data)
    // ========================================================================================

    private async Task<List<ClassificationEvidence>> ScanCodePatternsAsync(Guid analysisId, CancellationToken ct)
    {
        var evidences = new List<ClassificationEvidence>();

        // Check if analysis exists
        var analysisExists = await _context.Analyses
            .AsNoTracking()
            .AnyAsync(a => a.Id == analysisId, ct);

        if (!analysisExists)
        {
            _logger.LogDebug("No analysis data found for {AnalysisId}, skipping Layer 2", analysisId);
            return evidences;
        }

        // Check for API controllers / endpoints
        var hasEndpoints = await _context.ApiEndpoints
            .AsNoTracking()
            .AnyAsync(e => e.AnalysisId == analysisId, ct);

        if (hasEndpoints)
        {
            var endpointCount = await _context.ApiEndpoints
                .AsNoTracking()
                .CountAsync(e => e.AnalysisId == analysisId, ct);

            evidences.Add(new ClassificationEvidence
            {
                FilePath = "(analysis data)",
                Reason = $"{endpointCount} API endpoint(s) detected — indicates API backend",
                Layer = 2
            });
        }

        // Check for [ApiController] attribute, DbContext subclass, etc. via symbols
        var symbols = await _context.CodeSymbols
            .AsNoTracking()
            .Include(s => s.SourceFile)
            .Where(s => s.SourceFile.AnalysisId == analysisId &&
                       (s.SymbolType == SymbolType.Class || s.SymbolType == SymbolType.Interface))
            .ToListAsync(ct);

        // DbContext detection
        var dbContextSymbols = symbols
            .Where(s => s.Name.EndsWith("DbContext", StringComparison.OrdinalIgnoreCase) ||
                       s.FullName.Contains("DbContext", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dbContextSymbols.Count > 0)
        {
            foreach (var dbCtx in dbContextSymbols)
            {
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = dbCtx.SourceFile.Path,
                    Reason = $"DbContext subclass '{dbCtx.Name}' found — indicates database usage",
                    Layer = 2
                });
            }
        }

        // Check for DatabaseEntities (DbSet<> detection)
        var hasDatabaseEntities = await _context.DatabaseEntities
            .AsNoTracking()
            .AnyAsync(d => d.AnalysisId == analysisId, ct);

        if (hasDatabaseEntities)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "(analysis data)",
                Reason = "Database entities (DbSet<>) detected — confirms database layer",
                Layer = 2
            });
        }

        // Controller detection (classes ending with "Controller")
        var controllerSymbols = symbols
            .Where(s => s.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (controllerSymbols.Count > 0)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = controllerSymbols.First().SourceFile.Path,
                Reason = $"{controllerSymbols.Count} controller class(es) detected (e.g., '{controllerSymbols.First().Name}')",
                Layer = 2
            });
        }

        // Entry point detection (Program class / Main method)
        var hasEntryPoint = symbols
            .Any(s => s.Name.Equals("Program", StringComparison.OrdinalIgnoreCase) &&
                     s.SymbolType == SymbolType.Class);

        if (hasEntryPoint)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = symbols.First(s => s.Name.Equals("Program", StringComparison.OrdinalIgnoreCase)).SourceFile.Path,
                Reason = controllerSymbols.Count == 0
                    ? "Program class found without controllers — indicates Console/CLI application"
                    : "Program class found — application entry point",
                Layer = 2
            });
        }

        // Library detection: no entry point, only public classes/interfaces
        if (!hasEntryPoint && controllerSymbols.Count == 0 && !hasEndpoints)
        {
            var publicClassCount = symbols.Count(s => s.SymbolType == SymbolType.Class || s.SymbolType == SymbolType.Interface);
            if (publicClassCount > 0)
            {
                evidences.Add(new ClassificationEvidence
                {
                    FilePath = "(analysis data)",
                    Reason = $"{publicClassCount} public class/interface(s) found without entry point or controllers — suggests library",
                    Layer = 2
                });
            }
        }

        // Frontend route detection (Next.js app/ or pages/ directory)
        var sourceFiles = await _context.SourceFiles
            .AsNoTracking()
            .Where(f => f.AnalysisId == analysisId)
            .Select(f => f.Path)
            .ToListAsync(ct);

        var hasAppDir = sourceFiles.Any(f =>
            f.Contains("/app/", StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith("app/", StringComparison.OrdinalIgnoreCase));

        var hasPagesDir = sourceFiles.Any(f =>
            f.Contains("/pages/", StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith("pages/", StringComparison.OrdinalIgnoreCase));

        if (hasAppDir || hasPagesDir)
        {
            var dirType = hasAppDir ? "app/" : "pages/";
            evidences.Add(new ClassificationEvidence
            {
                FilePath = dirType,
                Reason = $"Next.js route directory '{dirType}' detected — indicates frontend with routing",
                Layer = 2
            });
        }

        // ASP.NET Core MVC detection (Razor views, Areas, ViewComponents, wwwroot)
        var razorFiles = sourceFiles.Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                                               f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)).ToList();
        if (razorFiles.Count > 0)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = razorFiles.First(),
                Reason = $"{razorFiles.Count} Razor view (.cshtml) file(s) detected — indicates ASP.NET Core MVC (Server-Side Rendering)",
                Layer = 2
            });
        }

        var hasAreas = sourceFiles.Any(f => f.Contains("/Areas/", StringComparison.OrdinalIgnoreCase) ||
                                           f.StartsWith("Areas/", StringComparison.OrdinalIgnoreCase));
        if (hasAreas)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "Areas/",
                Reason = "ASP.NET Core Area partition detected (e.g., Admin Area)",
                Layer = 2
            });
        }

        var viewComponents = symbols.Where(s => s.Name.EndsWith("ViewComponent", StringComparison.OrdinalIgnoreCase)).ToList();
        if (viewComponents.Count > 0)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = viewComponents.First().SourceFile.Path,
                Reason = $"{viewComponents.Count} ViewComponent(s) detected ({string.Join(", ", viewComponents.Select(v => v.Name))})",
                Layer = 2
            });
        }

        var hasWwwroot = sourceFiles.Any(f => f.Contains("/wwwroot/", StringComparison.OrdinalIgnoreCase) ||
                                              f.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase));
        if (hasWwwroot)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "wwwroot/",
                Reason = "wwwroot static assets directory detected (CSS, JS, Images, Lib)",
                Layer = 2
            });
        }

        return evidences;
    }

    // ========================================================================================
    // LAYER 3: Component Relationships
    // ========================================================================================

    private async Task<List<ClassificationEvidence>> ScanRelationshipsAsync(Guid analysisId, CancellationToken ct)
    {
        var evidences = new List<ClassificationEvidence>();

        // Check project-level references
        var projectDeps = await _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId &&
                       d.DependencyType == DependencyType.ProjectReference)
            .ToListAsync(ct);

        if (projectDeps.Count > 0)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "(analysis data)",
                Reason = $"{projectDeps.Count} project reference(s) found — indicates multi-layer architecture",
                Layer = 3
            });
        }

        // Count total projects
        var projectCount = await _context.Projects
            .AsNoTracking()
            .CountAsync(p => p.AnalysisId == analysisId, ct);

        if (projectCount > 1)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "(analysis data)",
                Reason = $"{projectCount} projects detected in the repository — possible monorepo/multi-layer",
                Layer = 3
            });
        }

        // Check for interface implementations (Inherits, Implements)
        var implementsDeps = await _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId &&
                       (d.DependencyType == DependencyType.Implements ||
                        d.DependencyType == DependencyType.Inherits))
            .CountAsync(ct);

        if (implementsDeps > 0)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "(analysis data)",
                Reason = $"{implementsDeps} inheritance/implementation relationship(s) found",
                Layer = 3
            });
        }

        // Check for method calls
        var callsDeps = await _context.Dependencies
            .AsNoTracking()
            .Where(d => d.AnalysisId == analysisId &&
                       d.DependencyType == DependencyType.Calls)
            .CountAsync(ct);

        if (callsDeps > 0)
        {
            evidences.Add(new ClassificationEvidence
            {
                FilePath = "(analysis data)",
                Reason = $"{callsDeps} method call relationship(s) detected between components",
                Layer = 3
            });
        }

        return evidences;
    }

    // ========================================================================================
    // Classification Decision Logic
    // ========================================================================================

    private static (RepositoryType Type, DetectionConfidence Confidence, string Summary) Classify(
        List<ClassificationEvidence> layer1,
        List<ClassificationEvidence> layer2,
        List<ClassificationEvidence> layer3,
        HashSet<string> detectedLanguages)
    {
        // Check for unsupported languages first
        var supportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C#", "TypeScript", "JavaScript", "Java", "Python", "Go", "PHP", "Rust", "Kotlin"
        };
        var hasOnlyUnsupported = detectedLanguages.Count > 0 &&
                                  !detectedLanguages.Any(l => supportedLanguages.Contains(l));

        if (hasOnlyUnsupported)
        {
            return (
                RepositoryType.Unsupported,
                DetectionConfidence.High,
                $"Repository uses unsupported language(s): {string.Join(", ", detectedLanguages)}. " +
                "Deep analysis is not available. Only directory tree and file statistics are shown."
            );
        }

        // No evidence at all
        if (layer1.Count == 0 && layer2.Count == 0 && layer3.Count == 0)
        {
            return (
                RepositoryType.Unsupported,
                DetectionConfidence.Unknown,
                "No recognizable project files or code patterns detected."
            );
        }

        // Gather signals
        bool hasCsproj = layer1.Any(e => e.Reason.Contains(".NET project file") || e.Reason.Contains(".sln"));
        bool hasPackageJson = layer1.Any(e => e.Reason.Contains("package.json"));
        bool hasNextJs = layer1.Any(e => e.Reason.Contains("Next.js"));
        bool hasReact = layer1.Any(e => e.Reason.Contains("React"));
        bool hasExpress = layer1.Any(e => e.Reason.Contains("Express"));
        bool hasVue = layer1.Any(e => e.Reason.Contains("Vue.js"));
        bool hasAngular = layer1.Any(e => e.Reason.Contains("Angular"));

        bool hasApiEndpoints = layer2.Any(e => e.Reason.Contains("API endpoint"));
        bool hasControllers = layer2.Any(e => e.Reason.Contains("controller class"));
        bool hasDbContext = layer2.Any(e => e.Reason.Contains("DbContext"));
        bool hasEntryPoint = layer2.Any(e => e.Reason.Contains("Program class"));
        bool suggestsLibrary = layer2.Any(e => e.Reason.Contains("suggests library"));
        bool hasFrontendRoutes = layer2.Any(e => e.Reason.Contains("route directory"));

        bool hasMultipleProjects = layer3.Any(e => e.Reason.Contains("projects detected"));
        bool hasProjectReferences = layer3.Any(e => e.Reason.Contains("project reference"));

        // Count project files across different tech stacks
        int csprojCount = layer1.Count(e => e.Reason.Contains(".NET project file"));
        int packageJsonCount = layer1.Count(e => e.Reason.Contains("package.json found"));

        // === MONOREPO detection ===
        // Multiple projects of the same or different types, OR has both frontend and backend signals
        bool isMonorepo = false;
        if ((csprojCount > 1 || packageJsonCount > 1 || (hasCsproj && hasPackageJson)) && hasMultipleProjects)
        {
            isMonorepo = true;
        }

        if (isMonorepo)
        {
            var confidence = hasProjectReferences ? DetectionConfidence.High : DetectionConfidence.Medium;
            return (
                RepositoryType.Monorepo,
                confidence,
                $"Monorepo/multi-project repository with {string.Join(", ", detectedLanguages)} detected. " +
                "Contains multiple sub-projects that may include frontend, backend, and shared libraries."
            );
        }

        // === API BACKEND / MVC MONOLITH detection ===
        if (hasApiEndpoints || hasControllers)
        {
            var isMvc = layer2.Any(e => e.Reason.Contains("Razor view") || e.Reason.Contains("Area partition") || e.Reason.Contains("ViewComponent"));
            var confidence = (hasApiEndpoints && hasControllers && hasDbContext)
                ? DetectionConfidence.High
                : (hasApiEndpoints || hasControllers)
                    ? DetectionConfidence.Medium
                    : DetectionConfidence.Low;

            var dbNote = hasDbContext ? " Database layer detected." : "";
            var archSummary = isMvc
                ? "ASP.NET Core MVC monolith (Server-Side Rendering) with Razor Views, Admin Area, and ViewComponents."
                : "Request flow: Controller → Service → Repository → Database.";

            return (
                RepositoryType.ApiBackend,
                confidence,
                $"{(isMvc ? "ASP.NET Core MVC" : "API backend")} project ({string.Join(", ", detectedLanguages)}).{dbNote} {archSummary}"
            );
        }

        // === FRONTEND detection ===
        if (hasNextJs || hasReact || hasVue || hasAngular || hasFrontendRoutes)
        {
            var framework = hasNextJs ? "Next.js" : hasReact ? "React" : hasVue ? "Vue.js" : hasAngular ? "Angular" : "Frontend";
            var confidence = (hasFrontendRoutes && (hasNextJs || hasReact))
                ? DetectionConfidence.High
                : DetectionConfidence.Medium;

            return (
                RepositoryType.Frontend,
                confidence,
                $"{framework} frontend application. " +
                "Route map and component tree available for visualization."
            );
        }

        // === EXPRESS / Node.js backend ===
        if (hasExpress)
        {
            return (
                RepositoryType.ApiBackend,
                DetectionConfidence.Medium,
                "Express.js API backend. Endpoint listing available."
            );
        }

        // === JAVA / Spring Boot backend ===
        bool hasJava = detectedLanguages.Contains("Java");
        if (hasJava)
        {
            return (
                RepositoryType.ApiBackend,
                DetectionConfidence.High,
                $"Java Enterprise / Spring Boot application ({string.Join(", ", detectedLanguages)}). " +
                "Architecture: DispatcherServlet → Controller → Service → Repository → Database."
            );
        }

        // === PYTHON backend (FastAPI / Django / Flask) ===
        bool hasPython = detectedLanguages.Contains("Python");
        if (hasPython)
        {
            return (
                RepositoryType.ApiBackend,
                DetectionConfidence.High,
                $"Python application ({string.Join(", ", detectedLanguages)}). " +
                "Architecture: Client Request → Router/Views → Services/UseCases → Models/ORM → Database."
            );
        }

        // === GO backend (Gin / Fiber / Standard) ===
        bool hasGo = detectedLanguages.Contains("Go");
        if (hasGo)
        {
            return (
                RepositoryType.ApiBackend,
                DetectionConfidence.High,
                $"Go backend service ({string.Join(", ", detectedLanguages)}). " +
                "Architecture: HTTP Router → Handlers → Services/UseCases → Repositories/Database."
            );
        }

        // === PHP backend (Laravel / Symfony) ===
        bool hasPhp = detectedLanguages.Contains("PHP");
        if (hasPhp)
        {
            return (
                RepositoryType.ApiBackend,
                DetectionConfidence.High,
                $"PHP application ({string.Join(", ", detectedLanguages)}). " +
                "Architecture: Routing → Controllers → Eloquent Models → Views/Database."
            );
        }

        // === RUST backend ===
        bool hasRust = detectedLanguages.Contains("Rust");
        if (hasRust)
        {
            return (
                RepositoryType.ApiBackend,
                DetectionConfidence.High,
                $"Rust service ({string.Join(", ", detectedLanguages)}). " +
                "Architecture: Async HTTP Router → Handlers → Domain Logic → Storage."
            );
        }

        // === CLI detection ===
        if (hasEntryPoint && !hasControllers && !hasApiEndpoints && hasCsproj)
        {
            var confidence = layer3.Count > 0 ? DetectionConfidence.High : DetectionConfidence.Medium;
            return (
                RepositoryType.Cli,
                confidence,
                "Console/CLI application with entry point (Program.cs) but no API controllers."
            );
        }

        // === LIBRARY detection ===
        if (suggestsLibrary || (hasCsproj && !hasEntryPoint && !hasControllers))
        {
            var confidence = layer3.Count > 0 ? DetectionConfidence.Medium : DetectionConfidence.Low;
            return (
                RepositoryType.Library,
                confidence,
                "Library project — no entry point or API controllers. " +
                "Namespace and class hierarchy diagram available."
            );
        }

        // === Fallback ===
        if (detectedLanguages.Count == 0)
        {
            return (
                RepositoryType.Unsupported,
                DetectionConfidence.Unknown,
                "Unable to determine repository type. No supported language markers found."
            );
        }

        return (
            RepositoryType.Unsupported,
            DetectionConfidence.Low,
            $"Repository uses {string.Join(", ", detectedLanguages)} but could not be confidently classified."
        );
    }

    // ========================================================================================
    // Helpers
    // ========================================================================================

    private static bool IsIgnoredDirectory(string dirName)
    {
        return dirName.StartsWith('.') ||
               dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("build", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("vendor", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("__pycache__", StringComparison.OrdinalIgnoreCase);
    }
}
