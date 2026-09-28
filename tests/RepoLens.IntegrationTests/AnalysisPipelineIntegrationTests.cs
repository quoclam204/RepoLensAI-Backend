using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Models.Pipeline;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Acquisition;
using RepoLens.Infrastructure.Adapters.Analysis;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Pipeline;
using RepoLens.Infrastructure.Scanning;
using RepoLens.Infrastructure.Services;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.IntegrationTests;

public class AnalysisPipelineIntegrationTests : IDisposable
{
    private readonly string _testBaseDir;

    public AnalysisPipelineIntegrationTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "RepoLens_PipelineIntegration_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBaseDir))
        {
            try { Directory.Delete(_testBaseDir, recursive: true); } catch { }
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

    private static MemoryStream CreateSampleZipStream()
    {
        var memStream = new MemoryStream();
        using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // BillingService.csproj
            var csprojEntry = archive.CreateEntry("BillingService/BillingService.csproj");
            using (var writer = new StreamWriter(csprojEntry.Open(), Encoding.UTF8))
            {
                writer.Write(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>");
            }

            // InvoicesController.cs
            var fileEntry = archive.CreateEntry("BillingService/InvoicesController.cs");
            using (var writer = new StreamWriter(fileEntry.Open(), Encoding.UTF8))
            {
                writer.Write(@"namespace BillingService;

public class InvoicesController
{
    public string GetInvoice(int id) => $""Invoice_{id}"";
}");
            }
        }

        memStream.Position = 0;
        return memStream;
    }

    [Fact]
    public async Task Pipeline_ExecutesAllStages_TransitionsToCompleted_AndCleansWorkspace()
    {
        // 1. Arrange
        var dbName = "PipelineDb_" + Guid.NewGuid().ToString("N");
        using var context = CreateTestDbContext(dbName);

        var repoId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();

        var repoEntity = new Repository
        {
            Id = repoId,
            Name = "SampleRepo",
            SourceType = RepositorySourceType.ZipUpload,
            SourceLocation = "sample.zip",
            Status = RepositoryStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Repositories.Add(repoEntity);

        var analysisEntity = new RepoLens.Domain.Entities.Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Created,
            CurrentStage = "Validation",
            StartedAt = DateTimeOffset.UtcNow
        };
        context.Analyses.Add(analysisEntity);
        await context.SaveChangesAsync();

        var workspaceOptions = Options.Create(new WorkspaceOptions { BaseDirectory = _testBaseDir });
        var workspaceManager = new TemporaryWorkspaceManager(
            workspaceOptions,
            NullLogger<TemporaryWorkspaceManager>.Instance,
            NullLogger<TemporaryWorkspace>.Instance);

        var acquisitionOptions = Options.Create(new AcquisitionOptions());
        var zipSource = new ZipRepositorySource(acquisitionOptions, NullLogger<ZipRepositorySource>.Instance);
        var sources = new IRepositorySource[] { zipSource };

        var scanningOptions = Options.Create(new ScanningOptions());
        var scannerService = new FileScanner(
            scanningOptions,
            new IgnoreRules(),
            new SecretDetector(),
            new LanguageDetector(),
            new ProjectDetector(),
            NullLogger<FileScanner>.Instance);

        var analyzer = new RoslynRepositoryAnalyzerAdapter(NullLogger<RoslynRepositoryAnalyzerAdapter>.Instance);
        var persistenceService = new AnalysisPersistenceService(context, NullLogger<AnalysisPersistenceService>.Instance);

        var pipeline = new AnalysisPipeline(
            context,
            workspaceManager,
            sources,
            scannerService,
            acquisitionOptions,
            NullLogger<AnalysisPipeline>.Instance,
            analyzer,
            persistenceService);

        using var zipStream = CreateSampleZipStream();
        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: zipStream,
            FileName: "sample.zip",
            ContentLength: zipStream.Length);

        // 2. Act
        var result = await pipeline.ExecuteAsync(analysisId, request);

        // 3. Assert
        Assert.True(result.Success);
        Assert.Equal(AnalysisStatus.Completed, result.FinalStatus);
        Assert.Equal(AnalysisStage.Completed, result.FinalStage);

        var finalAnalysis = await context.Analyses
            .Include(a => a.Projects)
            .Include(a => a.SourceFiles)
            .FirstOrDefaultAsync(a => a.Id == analysisId);

        Assert.NotNull(finalAnalysis);
        Assert.Equal(AnalysisStatus.Completed, finalAnalysis.Status);
        Assert.Equal(AnalysisStage.Completed.ToString(), finalAnalysis.CurrentStage);
        Assert.NotNull(finalAnalysis.CompletedAt);

        // Assert workspace cleaned up
        var remainingWorkspace = await workspaceManager.GetWorkspaceAsync(analysisId);
        Assert.Null(remainingWorkspace);
    }
}
