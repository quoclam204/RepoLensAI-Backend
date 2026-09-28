using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Acquisition;
using RepoLens.Infrastructure.Adapters.Analysis;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Pipeline;
using RepoLens.Infrastructure.Scanning;
using RepoLens.Infrastructure.Services;
using RepoLens.Infrastructure.Storage;
using AnalysisEntity = RepoLens.Domain.Entities.Analysis;

namespace RepoLens.IntegrationTests;

/// <summary>
/// End-to-End Pipeline Integration Tests (T101, T122).
/// Validates the full flow: ZIP upload -> Acquisition -> Scan -> Roslyn Static Analysis ->
/// Chunk Embeddings -> Database Persistence -> Query APIs -> Grounded RAG Chat.
/// </summary>
public class EndToEndPipelineIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _workspaceBase;

    public EndToEndPipelineIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RepoLens_E2E_" + Guid.NewGuid().ToString("N"));
        _workspaceBase = Path.Combine(_testDir, "workspaces");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_workspaceBase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private static RepoLensDbContext CreateTestDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new RepoLensDbContext(options);
    }

    [Fact]
    public async Task CompleteE2EFlow_ZipUpload_Analysis_Persistence_And_Chat_Succeeds()
    {
        // 1. Arrange: Create a sample C# repository and pack into ZIP
        var sourceDir = Path.Combine(_testDir, "source_repo");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, "OrderApp.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        File.WriteAllText(Path.Combine(sourceDir, "Order.cs"),
            """
            namespace OrderApp.Models;

            public class Order
            {
                public int Id { get; set; }
                public string CustomerName { get; set; } = string.Empty;
                public decimal TotalAmount { get; set; }
            }
            """);

        File.WriteAllText(Path.Combine(sourceDir, "OrdersController.cs"),
            """
            using Microsoft.AspNetCore.Mvc;
            using OrderApp.Models;

            namespace OrderApp.Controllers;

            [ApiController]
            [Route("api/[controller]")]
            public class OrdersController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetOrders() => Ok(new[] { new Order { Id = 1, CustomerName = "Alice", TotalAmount = 99.9m } });
            }
            """);

        var zipPath = Path.Combine(_testDir, "OrderApp.zip");
        ZipFile.CreateFromDirectory(sourceDir, zipPath);

        // 2. Setup DbContext and Services
        var dbName = "RepoLens_E2E_" + Guid.NewGuid().ToString("N");
        using var dbContext = CreateTestDbContext(dbName);

        var repo = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "OrderApp",
            SourceType = RepositorySourceType.ZipUpload,
            SourceLocation = "OrderApp.zip",
            Status = RepositoryStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Repositories.Add(repo);

        var analysis = new AnalysisEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = repo.Id,
            Status = AnalysisStatus.Created,
            CurrentStage = "Validation",
            StartedAt = DateTimeOffset.UtcNow
        };
        dbContext.Analyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var workspaceOptions = Options.Create(new WorkspaceOptions { BaseDirectory = _workspaceBase });
        var workspaceManager = new TemporaryWorkspaceManager(
            workspaceOptions,
            NullLogger<TemporaryWorkspaceManager>.Instance,
            NullLogger<TemporaryWorkspace>.Instance);

        var acquisitionOptions = Options.Create(new AcquisitionOptions());
        var zipSource = new ZipRepositorySource(acquisitionOptions, NullLogger<ZipRepositorySource>.Instance);
        var gitSource = new GitRepositorySource(acquisitionOptions, NullLogger<GitRepositorySource>.Instance);

        var scannerOptions = Options.Create(new ScanningOptions());
        var ignoreRules = new IgnoreRules();
        var secretDetector = new SecretDetector();
        var langDetector = new LanguageDetector();
        var projDetector = new ProjectDetector();
        var scannerService = new FileScanner(scannerOptions, ignoreRules, secretDetector, langDetector, projDetector, NullLogger<FileScanner>.Instance);

        var repositoryAnalyzer = new RoslynRepositoryAnalyzerAdapter(NullLogger<RoslynRepositoryAnalyzerAdapter>.Instance);
        var persistenceService = new AnalysisPersistenceService(dbContext, NullLogger<AnalysisPersistenceService>.Instance);

        var fakeEmbeddingProvider = new DeterministicTestEmbeddingProvider();
        var chunkEmbeddingService = new ChunkEmbeddingService(fakeEmbeddingProvider);

        var pipeline = new AnalysisPipeline(
            dbContext,
            workspaceManager,
            [zipSource, gitSource],
            scannerService,
            repositoryAnalyzer,
            persistenceService,
            chunkEmbeddingService,
            acquisitionOptions,
            NullLogger<AnalysisPipeline>.Instance);

        // 3. Act: Execute the Pipeline
        using var zipStream = File.OpenRead(zipPath);
        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: zipStream,
            FileName: "OrderApp.zip",
            ContentLength: zipStream.Length);

        var pipelineResult = await pipeline.ExecuteAsync(analysis.Id, request);

        // 4. Assert Pipeline Status
        Assert.True(pipelineResult.Success);
        Assert.Equal(AnalysisStatus.Completed, pipelineResult.FinalStatus);

        // Reload Analysis from DB
        var savedAnalysis = await dbContext.Analyses.FirstAsync(a => a.Id == analysis.Id);
        Assert.Equal(AnalysisStatus.Completed, savedAnalysis.Status);
        Assert.Equal("Completed", savedAnalysis.CurrentStage);
        Assert.NotNull(savedAnalysis.CompletedAt);

        // 5. Assert Persisted Data
        var projects = await dbContext.Projects.Where(p => p.AnalysisId == analysis.Id).ToListAsync();
        Assert.Single(projects);
        Assert.Equal("OrderApp", projects[0].Name);

        var files = await dbContext.SourceFiles.Where(f => f.AnalysisId == analysis.Id).ToListAsync();
        Assert.True(files.Count >= 2);

        var symbols = await dbContext.CodeSymbols.Where(s => s.SourceFile.AnalysisId == analysis.Id).ToListAsync();
        Assert.Contains(symbols, s => s.Name == "Order");
        Assert.Contains(symbols, s => s.Name == "OrdersController");

        var endpoints = await dbContext.ApiEndpoints.Where(e => e.AnalysisId == analysis.Id).ToListAsync();
        Assert.Contains(endpoints, e => e.Method == "GET");

        var chunks = await dbContext.DocumentChunks.Where(c => c.AnalysisId == analysis.Id).ToListAsync();
        Assert.NotEmpty(chunks);

        // 6. Test Query API (ArchitectureService)
        var architectureService = new ArchitectureService(dbContext);
        var archResult = await architectureService.GetArchitectureAsync(analysis.Id);
        Assert.NotNull(archResult);
        Assert.NotEmpty(archResult.Nodes);

        // 7. Test RAG Chat
        var fakeRetriever = new InMemoryVectorRetriever(chunks);
        var fakeAiProvider = new DeterministicAiProvider("The OrderApp service provides an OrdersController with a GET endpoint for orders.");

        var ragService = new RagService(
            fakeAiProvider,
            fakeEmbeddingProvider,
            fakeRetriever);

        var chatResult = await ragService.AnswerQuestionAsync(analysis.Id, "What does OrdersController do?");
        Assert.True(chatResult.HasSufficientEvidence);
        Assert.NotEmpty(chatResult.Answer);
        Assert.NotEmpty(chatResult.Evidence);
    }

    private sealed class DeterministicTestEmbeddingProvider : IEmbeddingProvider
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            var vectors = inputs.Select(_ =>
            {
                var v = new float[1536];
                Array.Fill(v, 0.05f);
                return v;
            }).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(vectors);
        }
    }

    private sealed class InMemoryVectorRetriever : IVectorChunkRetriever
    {
        private readonly List<DocumentChunk> _chunks;

        public InMemoryVectorRetriever(List<DocumentChunk> chunks) => _chunks = chunks;

        public Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
            Guid analysisId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            var results = _chunks
                .Where(c => c.AnalysisId == analysisId)
                .Take(topK)
                .Select(c => new VectorChunkSearchResult(
                    ChunkId: c.Id,
                    AnalysisId: c.AnalysisId,
                    SourceFileId: c.SourceFileId,
                    FilePath: "Order.cs",
                    Symbol: "OrdersController",
                    StartLine: c.StartLine,
                    EndLine: c.EndLine,
                    Content: c.Content,
                    TokenCount: c.TokenCount,
                    ChunkIndex: c.ChunkIndex,
                    EvidenceId: c.EvidenceId ?? Guid.NewGuid(),
                    ConfidenceScore: c.ConfidenceScore,
                    CosineDistance: 0.1,
                    SimilarityScore: 0.9))
                .ToList();

            return Task.FromResult<IReadOnlyList<VectorChunkSearchResult>>(results);
        }
    }

    private sealed class DeterministicAiProvider : IAiProvider
    {
        private readonly string _response;
        public DeterministicAiProvider(string response) => _response = response;

        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = _response,
                Confidence = AiConfidenceLevel.High,
                Evidence = []
            });
        }
    }
}
