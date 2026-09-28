using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;

    public CustomWebApplicationFactory()
    {
        _databaseName = "RepoLens_IntegrationTest_" + Guid.NewGuid().ToString("N");
    }

    public string DatabaseName => _databaseName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<RepoLensDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(RepoLensDbContext) ||
                (d.ServiceType.Namespace != null && (d.ServiceType.Namespace.StartsWith("Microsoft.EntityFrameworkCore") || d.ServiceType.Namespace.StartsWith("Npgsql"))) ||
                (d.ImplementationType != null && d.ImplementationType.Namespace != null && (d.ImplementationType.Namespace.StartsWith("Microsoft.EntityFrameworkCore") || d.ImplementationType.Namespace.StartsWith("Npgsql")))
            ).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Create an isolated service provider for InMemory database
            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<RepoLensDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            // Register dummy AI and Embedding providers so DI container validates properly in test environment
            services.AddSingleton<IAiProvider, DummyAiProvider>();
            services.AddSingleton<IEmbeddingProvider, DummyEmbeddingProvider>();
        });
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    public async Task SeedAsync(Func<RepoLensDbContext, Task> seedAction)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RepoLensDbContext>();
        await seedAction(context);
    }

    private sealed class DummyAiProvider : IAiProvider
    {
        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = "Integration test dummy answer",
                Confidence = AiConfidenceLevel.Medium,
                Evidence = []
            });
        }
    }

    private sealed class DummyEmbeddingProvider : IEmbeddingProvider
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<float[]> result = inputs.Select(_ => new float[1536]).ToList();
            return Task.FromResult(result);
        }
    }
}
