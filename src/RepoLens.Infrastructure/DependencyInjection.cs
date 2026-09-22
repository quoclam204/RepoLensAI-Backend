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
                b.MigrationsAssembly(typeof(RepoLensDbContext).Assembly.FullName)));

        return services;
    }
}
