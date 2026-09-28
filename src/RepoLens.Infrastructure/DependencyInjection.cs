using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepoLens.Application.Abstractions;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        services.AddDbContext<RepoLensDbContext>(options =>
            options.UseNpgsql(connectionString, b =>
                b.UseVector()
                .MigrationsAssembly(typeof(RepoLensDbContext).Assembly.FullName)));

        // Register Workspace Management (T031)
        services.Configure<WorkspaceOptions>(opts =>
        {
            var section = configuration.GetSection(WorkspaceOptions.SectionName);
            var baseDir = section[nameof(WorkspaceOptions.BaseDirectory)];
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                opts.BaseDirectory = baseDir;
            }
        });
        services.AddSingleton<ITemporaryWorkspaceManager, TemporaryWorkspaceManager>();

        // Register Person 1: Repository Acquisition (T028 - T030)
        services.Configure<RepoLens.Infrastructure.Acquisition.AcquisitionOptions>(configuration.GetSection(RepoLens.Infrastructure.Acquisition.AcquisitionOptions.SectionName));
        services.AddScoped<RepoLens.Application.Abstractions.IRepositorySource, RepoLens.Infrastructure.Acquisition.GitRepositorySource>();
        services.AddScoped<RepoLens.Application.Abstractions.IRepositorySource, RepoLens.Infrastructure.Acquisition.ZipRepositorySource>();

        // Register Person 1: Repository Scanner & Detectors (T032 - T036)
        services.Configure<RepoLens.Infrastructure.Scanning.ScanningOptions>(configuration.GetSection(RepoLens.Infrastructure.Scanning.ScanningOptions.SectionName));
        services.AddSingleton<RepoLens.Infrastructure.Scanning.IgnoreRules>();
        services.AddSingleton<RepoLens.Infrastructure.Scanning.SecretDetector>();
        services.AddSingleton<RepoLens.Infrastructure.Scanning.LanguageDetector>();
        services.AddSingleton<RepoLens.Infrastructure.Scanning.ProjectDetector>();
        services.AddScoped<RepoLens.Application.Abstractions.IScannerService, RepoLens.Infrastructure.Scanning.FileScanner>();

        // Register Pipeline Orchestration & Background Worker (T052 - T054)
        services.AddSingleton<RepoLens.Application.Abstractions.IAnalysisQueue, RepoLens.Infrastructure.Background.ChannelAnalysisQueue>();
        services.AddHostedService<RepoLens.Infrastructure.Background.AnalysisBackgroundWorker>();
        services.AddScoped<RepoLens.Application.Abstractions.IAnalysisPipeline, RepoLens.Infrastructure.Pipeline.AnalysisPipeline>();

        // Register Query and Command Services (T060 - T069)
        services.AddScoped<RepoLens.Application.Abstractions.IAnalysisService, RepoLens.Infrastructure.Services.AnalysisService>();
        services.AddScoped<RepoLens.Application.Abstractions.IArchitectureService, RepoLens.Infrastructure.Services.ArchitectureService>();
        services.AddScoped<RepoLens.Application.Abstractions.IDependencyService, RepoLens.Infrastructure.Services.DependencyService>();
        services.AddScoped<RepoLens.Application.Abstractions.IApiEndpointService, RepoLens.Infrastructure.Services.ApiEndpointService>();
        services.AddScoped<RepoLens.Application.Abstractions.IDatabaseModelService, RepoLens.Infrastructure.Services.DatabaseModelService>();
        services.AddScoped<RepoLens.Application.Abstractions.IFileService, RepoLens.Infrastructure.Services.FileService>();
        services.AddScoped<RepoLens.Application.Abstractions.ISymbolService, RepoLens.Infrastructure.Services.SymbolService>();
        services.AddScoped<RepoLens.Application.Abstractions.IEvidenceService, RepoLens.Infrastructure.Services.EvidenceService>();
        services.AddScoped<RepoLens.Application.Abstractions.IAnalysisPersistenceService, RepoLens.Infrastructure.Services.AnalysisPersistenceService>();
        // T085: chunk embedding application service (explicit composition Analyze -> Embed -> Persist).
        services.AddScoped<RepoLens.Application.Abstractions.AI.IChunkEmbeddingService, RepoLens.Application.Services.ChunkEmbeddingService>();
        services.AddScoped<RepoLens.Application.Abstractions.IRepositoryAnalyzer, RepoLens.Infrastructure.Adapters.Analysis.RoslynRepositoryAnalyzerAdapter>();
        services.AddScoped<RepoLens.Application.Abstractions.IArchifyAdapter, RepoLens.Application.Services.ArchifyAdapter>();
        services.AddScoped<RepoLens.Application.Abstractions.IEvidenceRetriever, RepoLens.Infrastructure.Services.EvidenceRetriever>();
        // T086: Vector retrieval service for document chunks (pgvector cosine similarity)
        services.AddScoped<RepoLens.Application.Abstractions.IVectorChunkRetriever, RepoLens.Infrastructure.Services.VectorChunkRetriever>();
        // T087: Evidence-grounded RAG service
        services.AddScoped<RepoLens.Application.Services.RagService>();
        services.AddScoped<RepoLens.Application.Abstractions.AI.IRagService>(sp => sp.GetRequiredService<RepoLens.Application.Services.RagService>());
        services.AddScoped<RepoLens.Application.Abstractions.AI.IEvidenceGroundedRagService>(sp => sp.GetRequiredService<RepoLens.Application.Services.RagService>());
        // T088: AI evidence validation service
        services.AddScoped<RepoLens.Application.Abstractions.AI.IAiEvidenceValidator, RepoLens.Application.Services.AiEvidenceValidator>();
        // T089: AI confidence evaluation service
        services.AddScoped<RepoLens.Application.Abstractions.AI.IAiConfidenceCalculator, RepoLens.Application.Services.AiConfidenceCalculator>();

        // T081 & T082: AI & Embedding Providers and Options
        services.AddSingleton<HttpClient>();
        services.Configure<RepoLens.Infrastructure.Ai.AiOptions>(configuration.GetSection(RepoLens.Infrastructure.Ai.AiOptions.SectionName));
        services.Configure<RepoLens.Infrastructure.Ai.EmbeddingOptions>(configuration.GetSection(RepoLens.Infrastructure.Ai.EmbeddingOptions.SectionName));

        services.AddScoped<RepoLens.Application.Abstractions.AI.IEmbeddingProvider>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RepoLens.Infrastructure.Ai.EmbeddingOptions>>().Value;
            if (string.Equals(options.Provider, "OpenAi", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return ActivatorUtilities.CreateInstance<RepoLens.Infrastructure.Ai.OpenAiEmbeddingProvider>(sp);
            }
            return ActivatorUtilities.CreateInstance<RepoLens.Infrastructure.Ai.DeterministicEmbeddingProvider>(sp);
        });

        services.AddScoped<RepoLens.Application.Abstractions.AI.IAiProvider>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RepoLens.Infrastructure.Ai.AiOptions>>().Value;
            if (string.Equals(options.Provider, "OpenAi", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return ActivatorUtilities.CreateInstance<RepoLens.Infrastructure.Ai.OpenAiProvider>(sp);
            }
            return ActivatorUtilities.CreateInstance<RepoLens.Infrastructure.Ai.DeterministicAiProvider>(sp);
        });

        // T111: MemoryCache for safe query caching
        services.AddMemoryCache();

        // T112: Observability & Metrics
        services.AddSingleton<RepoLens.Application.Abstractions.IAnalysisMetrics, RepoLens.Infrastructure.Observability.AnalysisMetrics>();

        return services;
    }
}
