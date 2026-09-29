using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Services;
using RepoLens.Infrastructure;

namespace RepoLens.UnitTests.DependencyInjection;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_ResolvesAllCoreServicesWithoutExceptions()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=postgres;Password=postgres",
                ["Ai:Provider"] = "Deterministic",
                ["Embeddings:Provider"] = "Deterministic"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        // Act
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();

        // Assert - verify critical interfaces can be resolved
        var aiProvider = scope.ServiceProvider.GetRequiredService<IAiProvider>();
        var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
        var chunkEmbeddingService = scope.ServiceProvider.GetRequiredService<IChunkEmbeddingService>();
        var ragService = scope.ServiceProvider.GetRequiredService<IRagService>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IAnalysisPipeline>();
        var analysisQueue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();

        Assert.NotNull(aiProvider);
        Assert.NotNull(embeddingProvider);
        Assert.NotNull(chunkEmbeddingService);
        Assert.NotNull(ragService);
        Assert.NotNull(pipeline);
        Assert.NotNull(analysisQueue);
    }
}
