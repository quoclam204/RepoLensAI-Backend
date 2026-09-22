using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Adapters.Analysis;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Services;

namespace RepoLens.IntegrationTests;

public class AnalysisPersistenceIntegrationTests : IDisposable
{
    private readonly string _fixtureDir;

    public AnalysisPersistenceIntegrationTests()
    {
        _fixtureDir = Path.Combine(Path.GetTempPath(), "RepoLens_PersistenceIntegration_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_fixtureDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_fixtureDir))
        {
            try { Directory.Delete(_fixtureDir, recursive: true); } catch { }
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
    public async Task EndToEnd_Analyze_Persist_Reload_PreservesAllEvidenceAndKnowledge()
    {
        // 1. Arrange: Create untrusted repository files on disk
        var projDir = Directory.CreateDirectory(Path.Combine(_fixtureDir, "src", "BillingService"));
        var controllerDir = Directory.CreateDirectory(Path.Combine(projDir.FullName, "Controllers"));

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(Path.Combine(projDir.FullName, "BillingService.csproj"), csprojContent);

        var controllerContent = @"using System;

namespace BillingService.Controllers;

public class InvoicesController
{
    public string GetInvoice(int id)
    {
        return $""Invoice_{id}"";
    }
}
";
        File.WriteAllText(Path.Combine(controllerDir.FullName, "InvoicesController.cs"), controllerContent);

        var dbName = "RepoLens_Integration_" + Guid.NewGuid().ToString("N");
        using var context = CreateTestDbContext(dbName);

        // Pre-create Repository and Analysis records as required by foreign keys
        var repoId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();

        var repoEntity = new Repository
        {
            Id = repoId,
            Name = "BillingRepository",
            SourceType = RepositorySourceType.ZipUpload,
            SourceLocation = _fixtureDir,
            Status = RepositoryStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Repositories.Add(repoEntity);

        var analysisEntity = new RepoLens.Domain.Entities.Analysis
        {
            Id = analysisId,
            RepositoryId = repoId,
            Status = AnalysisStatus.Analyzing,
            CurrentStage = "StaticAnalysis",
            StartedAt = DateTimeOffset.UtcNow
        };
        context.Analyses.Add(analysisEntity);
        await context.SaveChangesAsync();

        // 2. Act: Analyze repository with Roslyn adapter
        var adapter = new RoslynRepositoryAnalyzerAdapter(NullLogger<RoslynRepositoryAnalyzerAdapter>.Instance);
        var analysisResultModel = await adapter.AnalyzeAsync(_fixtureDir, analysisId);

        // 3. Persist via AnalysisPersistenceService
        var persistenceService = new AnalysisPersistenceService(context, NullLogger<AnalysisPersistenceService>.Instance);
        await persistenceService.PersistAnalysisResultAsync(analysisResultModel);

        // 4. Assert: Reload and verify from database
        var reloadedAnalysis = await context.Analyses
            .Include(a => a.Projects)
            .Include(a => a.SourceFiles)
                .ThenInclude(f => f.Symbols)
            .Include(a => a.Evidences)
            .Include(a => a.DocumentChunks)
            .FirstOrDefaultAsync(a => a.Id == analysisId);

        Assert.NotNull(reloadedAnalysis);
        Assert.Equal(AnalysisStatus.Completed, reloadedAnalysis.Status);

        // Projects
        Assert.Single(reloadedAnalysis.Projects);
        Assert.Equal("BillingService", reloadedAnalysis.Projects.First().Name);

        // SourceFiles
        Assert.Single(reloadedAnalysis.SourceFiles);
        Assert.Contains("InvoicesController.cs", reloadedAnalysis.SourceFiles.First().Path);

        // CodeSymbols via SourceFiles
        var allSymbols = reloadedAnalysis.SourceFiles.SelectMany(f => f.Symbols).ToList();
        Assert.NotEmpty(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "InvoicesController");
        Assert.Contains(allSymbols, s => s.Name == "GetInvoice");

        // Evidences
        Assert.NotEmpty(reloadedAnalysis.Evidences);
        foreach (var evi in reloadedAnalysis.Evidences)
        {
            Assert.False(string.IsNullOrWhiteSpace(evi.FilePath));
            Assert.True(evi.StartLine > 0);
            Assert.True(evi.EndLine >= evi.StartLine);
            Assert.False(string.IsNullOrWhiteSpace(evi.Description));
        }

        // DocumentChunks for RAG
        Assert.NotEmpty(reloadedAnalysis.DocumentChunks);
        foreach (var chunk in reloadedAnalysis.DocumentChunks)
        {
            Assert.False(string.IsNullOrWhiteSpace(chunk.Content));
            Assert.True(chunk.TokenCount > 0);
        }
    }
}
