using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepoLens.Infrastructure.Persistence;

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

        // Register Query and Command Services (T060 - T069)
        services.AddScoped<RepoLens.Application.Abstractions.IAnalysisService, RepoLens.Infrastructure.Services.AnalysisService>();
        services.AddScoped<RepoLens.Application.Abstractions.IArchitectureService, RepoLens.Infrastructure.Services.ArchitectureService>();
        services.AddScoped<RepoLens.Application.Abstractions.IDependencyService, RepoLens.Infrastructure.Services.DependencyService>();
        services.AddScoped<RepoLens.Application.Abstractions.IApiEndpointService, RepoLens.Infrastructure.Services.ApiEndpointService>();
        services.AddScoped<RepoLens.Application.Abstractions.IDatabaseModelService, RepoLens.Infrastructure.Services.DatabaseModelService>();
        services.AddScoped<RepoLens.Application.Abstractions.IFileService, RepoLens.Infrastructure.Services.FileService>();
        services.AddScoped<RepoLens.Application.Abstractions.ISymbolService, RepoLens.Infrastructure.Services.SymbolService>();
        services.AddScoped<RepoLens.Application.Abstractions.IEvidenceService, RepoLens.Infrastructure.Services.EvidenceService>();

        return services;
    }
}
