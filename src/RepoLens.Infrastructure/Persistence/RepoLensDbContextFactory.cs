using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory for RepoLensDbContext (T084).
/// Enables EF Core tooling (dotnet ef migrations, database update) to create
/// RepoLensDbContext instances without executing the WebApplication pipeline or
/// resolving application-layer dependencies.
/// Uses pure .NET BCL (System.Text.Json, Environment) without requiring external
/// ConfigurationBuilder packages in the Infrastructure project.
/// </summary>
public class RepoLensDbContextFactory : IDesignTimeDbContextFactory<RepoLensDbContext>
{
    public RepoLensDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Could not find a valid connection string for 'Postgres'. " +
                "Ensure 'ConnectionStrings:Postgres' is configured in appsettings.Development.json " +
                "or provided via the 'ConnectionStrings__Postgres' / 'POSTGRES_CONNECTION' environment variable.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<RepoLensDbContext>();
        optionsBuilder.UseNpgsql(connectionString, b =>
            b.UseVector()
             .MigrationsAssembly(typeof(RepoLensDbContext).Assembly.FullName));

        return new RepoLensDbContext(optionsBuilder.Options);
    }

    private static string? ResolveConnectionString()
    {
        // 1. Environment variable override (standard ASP.NET Core convention)
        var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            return envConn;
        }

        // 2. Read from RepoLens.Api appsettings files
        var apiDir = FindApiDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Try environment-specific first (e.g. appsettings.Development.json)
        var devSettings = Path.Combine(apiDir, $"appsettings.{environment}.json");
        var conn = ReadConnectionStringFromJsonFile(devSettings);
        if (!string.IsNullOrWhiteSpace(conn))
        {
            return conn;
        }

        // Fallback to base appsettings.json
        var baseSettings = Path.Combine(apiDir, "appsettings.json");
        conn = ReadConnectionStringFromJsonFile(baseSettings);
        if (!string.IsNullOrWhiteSpace(conn))
        {
            return conn;
        }

        return null;
    }

    private static string? ReadConnectionStringFromJsonFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connSection) &&
                connSection.TryGetProperty("Postgres", out var postgresProp))
            {
                var val = postgresProp.GetString();
                return string.IsNullOrWhiteSpace(val) ? null : val;
            }
        }
        catch
        {
            // Ignore parse errors and let the caller fall through or throw
        }

        return null;
    }

    private static string FindApiDirectory()
    {
        var current = Directory.GetCurrentDirectory();

        // 1. Current directory is already the API project
        if (File.Exists(Path.Combine(current, "RepoLens.Api.csproj")))
        {
            return current;
        }

        // 2. Running from solution root: src/RepoLens.Api
        var solutionRelative = Path.Combine(current, "src", "RepoLens.Api");
        if (Directory.Exists(solutionRelative) && File.Exists(Path.Combine(solutionRelative, "RepoLens.Api.csproj")))
        {
            return solutionRelative;
        }

        // 3. Running from src/RepoLens.Infrastructure: ../RepoLens.Api
        var siblingRelative = Path.Combine(current, "..", "RepoLens.Api");
        if (Directory.Exists(siblingRelative) && File.Exists(Path.Combine(siblingRelative, "RepoLens.Api.csproj")))
        {
            return Path.GetFullPath(siblingRelative);
        }

        return current;
    }
}
