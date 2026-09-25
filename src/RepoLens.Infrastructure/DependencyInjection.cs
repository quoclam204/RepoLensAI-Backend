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

        return services;
    }
}
