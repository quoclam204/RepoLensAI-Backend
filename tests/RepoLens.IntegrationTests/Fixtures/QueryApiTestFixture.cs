using AnalysisEntity = RepoLens.Domain.Entities.Analysis;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.IntegrationTests.Fixtures;

public record SeededCompletedAnalysisData(
    Guid RepositoryId,
    Guid AnalysisId,
    Guid ProjectId,
    Guid SourceFileId,
    Guid SymbolId,
    Guid DependencyId,
    Guid EndpointId,
    Guid DatabaseEntityId,
    Guid RelationshipId,
    Guid EvidenceId
);

public static class QueryApiTestFixture
{
    public static async Task<SeededCompletedAnalysisData> SeedCompletedAnalysisAsync(CustomWebApplicationFactory factory)
    {
        var repoId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var symbolId = Guid.NewGuid();
        var dependencyId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var relId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var repo = new Repository
            {
                Id = repoId,
                Name = "RepoLens-Sample",
                SourceType = RepositorySourceType.GitUrl,
                SourceLocation = "https://github.com/org/repolens-sample",
                Status = RepositoryStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Repositories.Add(repo);

            var analysis = new AnalysisEntity
            {
                Id = analysisId,
                RepositoryId = repoId,
                Status = AnalysisStatus.Completed,
                CurrentStage = "Completed",
                CommitHash = "abcdef1234567890",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                CompletedAt = DateTimeOffset.UtcNow
            };
            context.Analyses.Add(analysis);

            var project = new Project
            {
                Id = projectId,
                AnalysisId = analysisId,
                Name = "BillingService",
                Path = "src/BillingService",
                Language = "C#",
                ProjectType = "WebApi"
            };
            context.Projects.Add(project);

            var file = new SourceFile
            {
                Id = fileId,
                AnalysisId = analysisId,
                ProjectId = projectId,
                Path = "src/BillingService/Controllers/InvoicesController.cs",
                Language = "C#",
                Size = 2048,
                AnalysisStatus = FileAnalysisStatus.Analyzed
            };
            context.SourceFiles.Add(file);

            var symbol = new CodeSymbol
            {
                Id = symbolId,
                SourceFileId = fileId,
                Name = "InvoicesController",
                FullName = "BillingService.Controllers.InvoicesController",
                SymbolType = SymbolType.Class,
                StartLine = 10,
                EndLine = 60
            };
            context.CodeSymbols.Add(symbol);

            var evidence = new Evidence
            {
                Id = evidenceId,
                AnalysisId = analysisId,
                FilePath = "src/BillingService/Controllers/InvoicesController.cs",
                Symbol = "InvoicesController",
                StartLine = 10,
                EndLine = 20,
                EvidenceType = EvidenceType.Declaration,
                Description = "Defines InvoicesController class"
            };
            context.Evidences.Add(evidence);

            var dependency = new Dependency
            {
                Id = dependencyId,
                AnalysisId = analysisId,
                SourceId = projectId.ToString(),
                TargetId = "ExternalPaymentGateway",
                DependencyType = DependencyType.ProjectReference,
                EvidenceId = evidenceId
            };
            context.Dependencies.Add(dependency);

            var endpoint = new ApiEndpoint
            {
                Id = endpointId,
                AnalysisId = analysisId,
                ProjectId = projectId,
                Method = "GET",
                Route = "/api/invoices",
                Controller = "InvoicesController",
                Action = "GetInvoices",
                SymbolId = symbolId,
                EvidenceId = evidenceId
            };
            context.ApiEndpoints.Add(endpoint);

            var dbEntity = new DatabaseEntity
            {
                Id = entityId,
                AnalysisId = analysisId,
                Name = "Invoice",
                EntityType = "Table",
                SourceSymbolId = symbolId
            };
            context.DatabaseEntities.Add(dbEntity);

            var relationship = new DatabaseRelationship
            {
                Id = relId,
                AnalysisId = analysisId,
                SourceEntityId = entityId,
                TargetEntityId = entityId,
                RelationshipType = DatabaseRelationshipType.OneToMany,
                EvidenceId = evidenceId
            };
            context.DatabaseRelationships.Add(relationship);

            await context.SaveChangesAsync();
        });

        return new SeededCompletedAnalysisData(
            repoId,
            analysisId,
            projectId,
            fileId,
            symbolId,
            dependencyId,
            endpointId,
            entityId,
            relId,
            evidenceId
        );
    }

    public static async Task<Guid> SeedAnalyzingAnalysisAsync(CustomWebApplicationFactory factory)
    {
        var repoId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var repo = new Repository
            {
                Id = repoId,
                Name = "RepoLens-Analyzing",
                SourceType = RepositorySourceType.GitUrl,
                SourceLocation = "https://github.com/org/repolens-analyzing",
                Status = RepositoryStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Repositories.Add(repo);

            var analysis = new AnalysisEntity
            {
                Id = analysisId,
                RepositoryId = repoId,
                Status = AnalysisStatus.Analyzing,
                CurrentStage = "StaticAnalysis",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = null
            };
            context.Analyses.Add(analysis);

            await context.SaveChangesAsync();
        });

        return analysisId;
    }

    public static async Task<Guid> SeedFailedAnalysisAsync(CustomWebApplicationFactory factory)
    {
        var repoId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var repo = new Repository
            {
                Id = repoId,
                Name = "RepoLens-Failed",
                SourceType = RepositorySourceType.GitUrl,
                SourceLocation = "https://github.com/org/repolens-failed",
                Status = RepositoryStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Repositories.Add(repo);

            var analysis = new AnalysisEntity
            {
                Id = analysisId,
                RepositoryId = repoId,
                Status = AnalysisStatus.Failed,
                CurrentStage = "StaticAnalysis",
                Error = "Roslyn compiler failed to parse corrupt C# file.",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                CompletedAt = DateTimeOffset.UtcNow
            };
            context.Analyses.Add(analysis);

            await context.SaveChangesAsync();
        });

        return analysisId;
    }
}
