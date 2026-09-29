using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.DTOs.Persistence;
using RepoLens.Application.Models.Pipeline;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Acquisition;
using RepoLens.Infrastructure.Persistence;
using AnalysisEntity = RepoLens.Domain.Entities.Analysis;

namespace RepoLens.Infrastructure.Pipeline;

/// <summary>
/// Pipeline orchestrator and lifecycle coordinator (T052 - T054, FR-002, FR-003).
/// Coordinates repository acquisition, security validation, workspace management,
/// scanning, static analysis, embedding, persistence, and lifecycle transitions.
/// </summary>
public sealed class AnalysisPipeline : IAnalysisPipeline
{
    private readonly RepoLensDbContext _dbContext;
    private readonly ITemporaryWorkspaceManager _workspaceManager;
    private readonly IEnumerable<IRepositorySource> _sources;
    private readonly IScannerService _scannerService;
    private readonly IRepositoryAnalyzer? _repositoryAnalyzer;
    private readonly IAnalysisPersistenceService? _persistenceService;
    private readonly IChunkEmbeddingService? _chunkEmbeddingService;
    private readonly AcquisitionOptions _acquisitionOptions;
    private readonly ILogger<AnalysisPipeline> _logger;

    [ActivatorUtilitiesConstructor]
    public AnalysisPipeline(
        RepoLensDbContext dbContext,
        ITemporaryWorkspaceManager workspaceManager,
        IEnumerable<IRepositorySource> sources,
        IScannerService scannerService,
        IRepositoryAnalyzer repositoryAnalyzer,
        IAnalysisPersistenceService persistenceService,
        IChunkEmbeddingService chunkEmbeddingService,
        IOptions<AcquisitionOptions> acquisitionOptions,
        ILogger<AnalysisPipeline> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(nameof(workspaceManager));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _scannerService = scannerService ?? throw new ArgumentNullException(nameof(scannerService));
        _repositoryAnalyzer = repositoryAnalyzer;
        _persistenceService = persistenceService;
        _chunkEmbeddingService = chunkEmbeddingService;
        _acquisitionOptions = acquisitionOptions?.Value ?? new AcquisitionOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AnalysisPipeline(
        RepoLensDbContext dbContext,
        ITemporaryWorkspaceManager workspaceManager,
        IEnumerable<IRepositorySource> sources,
        IScannerService scannerService,
        IOptions<AcquisitionOptions> acquisitionOptions,
        ILogger<AnalysisPipeline> logger,
        IRepositoryAnalyzer? repositoryAnalyzer = null,
        IAnalysisPersistenceService? persistenceService = null,
        IChunkEmbeddingService? chunkEmbeddingService = null)
        : this(
            dbContext,
            workspaceManager,
            sources,
            scannerService,
            repositoryAnalyzer!,
            persistenceService!,
            chunkEmbeddingService!,
            acquisitionOptions,
            logger)
    {
    }

    /// <inheritdoc />
    public async Task<AnalysisPipelineResult> ExecuteAsync(
        Guid analysisId,
        RepositorySourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Starting pipeline execution for analysis {AnalysisId}", analysisId);

        var analysis = await _dbContext.Analyses.FirstOrDefaultAsync(a => a.Id == analysisId, cancellationToken);
        if (analysis == null)
        {
            _logger.LogError("Analysis with ID {AnalysisId} was not found in database", analysisId);
            return AnalysisPipelineResult.Failed(AnalysisStage.Validation, $"Analysis {analysisId} not found.");
        }

        ITemporaryWorkspace? workspace = null;

        try
        {
            // -------------------------------------------------------------
            // Stage 1: Validation
            // -------------------------------------------------------------
            await UpdateAnalysisStageAsync(analysis, AnalysisStatus.Cloning, AnalysisStage.Validation, cancellationToken);

            var (isValid, validationError) = RepositoryValidator.Validate(request, _acquisitionOptions);
            if (!isValid)
            {
                await MarkAnalysisFailedAsync(analysis, AnalysisStage.Validation, validationError!, cancellationToken);
                return AnalysisPipelineResult.Failed(AnalysisStage.Validation, validationError!);
            }

            // -------------------------------------------------------------
            // Stage 2: Repository Acquisition
            // -------------------------------------------------------------
            await UpdateAnalysisStageAsync(analysis, AnalysisStatus.Cloning, AnalysisStage.RepositoryAcquisition, cancellationToken);

            var sourceHandler = _sources.FirstOrDefault(s => s.CanHandle(request));
            if (sourceHandler == null)
            {
                var error = $"No acquisition source handler registered for request type: {request.Type}";
                await MarkAnalysisFailedAsync(analysis, AnalysisStage.RepositoryAcquisition, error, cancellationToken);
                return AnalysisPipelineResult.Failed(AnalysisStage.RepositoryAcquisition, error);
            }

            workspace = await _workspaceManager.CreateWorkspaceAsync(analysisId, cancellationToken);

            var acquisitionResult = await sourceHandler.AcquireAsync(request, workspace, cancellationToken);
            if (!acquisitionResult.Success)
            {
                var error = acquisitionResult.ErrorMessage ?? "Repository acquisition failed.";
                await MarkAnalysisFailedAsync(analysis, AnalysisStage.RepositoryAcquisition, error, cancellationToken);
                return AnalysisPipelineResult.Failed(AnalysisStage.RepositoryAcquisition, error);
            }

            // -------------------------------------------------------------
            // Stage 3: File Scanning & Language / Project Detection
            // -------------------------------------------------------------
            await UpdateAnalysisStageAsync(analysis, AnalysisStatus.Scanning, AnalysisStage.FileScanning, cancellationToken);

            var scanResult = await _scannerService.ScanAsync(analysisId, workspace.RootPath, cancellationToken);

            _logger.LogInformation(
                "Acquisition and scan completed for analysis {AnalysisId}. Found {Files} files, {Projects} projects.",
                analysisId, scanResult.TotalFiles, scanResult.DetectedProjects.Count);

            // -------------------------------------------------------------
            // Stage 4: Static Analysis (Roslyn + TS AST + Knowledge Graph)
            // -------------------------------------------------------------
            await UpdateAnalysisStageAsync(analysis, AnalysisStatus.Analyzing, AnalysisStage.StaticAnalysis, cancellationToken);

            var analysisResult = _repositoryAnalyzer != null
                ? await _repositoryAnalyzer.AnalyzeAsync(workspace.RootPath, analysisId, cancellationToken)
                : null;

            // -------------------------------------------------------------
            // Stage 5: Document Chunk Embeddings
            // -------------------------------------------------------------
            if (_chunkEmbeddingService != null && analysisResult != null && analysisResult.DocumentChunks.Count > 0)
            {
                try
                {
                    await UpdateAnalysisStageAsync(analysis, AnalysisStatus.Indexing, AnalysisStage.Embedding, cancellationToken);
                    var (embeddedResult, _) = await _chunkEmbeddingService.PopulateEmbeddingsAsync(analysisResult, cancellationToken);
                    analysisResult = embeddedResult;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Document chunk embedding failed or was skipped for analysis {AnalysisId}. Proceeding with persistence.", analysisId);
                }
            }

            // -------------------------------------------------------------
            // Stage 6: Persistence
            // -------------------------------------------------------------
            await UpdateAnalysisStageAsync(analysis, AnalysisStatus.Indexing, AnalysisStage.Persistence, cancellationToken);

            if (_persistenceService != null && analysisResult != null)
            {
                analysisResult.NewStatus = AnalysisStatus.Completed;
                analysisResult.CurrentStage = AnalysisStage.Completed.ToString();
                await _persistenceService.PersistAnalysisResultAsync(analysisResult, cancellationToken);
            }
            else
            {
                await PersistScanResultsAsync(analysis, scanResult, cancellationToken);
            }

            // -------------------------------------------------------------
            // Stage 7: Completion
            // -------------------------------------------------------------
            analysis.Status = AnalysisStatus.Completed;
            analysis.CurrentStage = AnalysisStage.Completed.ToString();
            analysis.CompletedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Pipeline completed successfully for analysis {AnalysisId}.", analysisId);
            return AnalysisPipelineResult.Succeeded(AnalysisStage.Completed, scanResult);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipeline execution was cancelled for analysis {AnalysisId}", analysisId);
            if (analysis != null)
            {
                var failedStage = Enum.TryParse<AnalysisStage>(analysis.CurrentStage, out var stg) ? stg : AnalysisStage.Validation;
                await MarkAnalysisFailedAsync(analysis, failedStage, "Operation was cancelled.", CancellationToken.None);
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure during pipeline execution for analysis {AnalysisId}", analysisId);
            var failedStage = AnalysisStage.Validation;
            if (analysis != null)
            {
                failedStage = Enum.TryParse<AnalysisStage>(analysis.CurrentStage, out var stg) ? stg : AnalysisStage.Validation;
                await MarkAnalysisFailedAsync(analysis, failedStage, $"Pipeline execution failed: {ex.Message}", CancellationToken.None);
            }
            return AnalysisPipelineResult.Failed(failedStage, ex.Message);
        }
        finally
        {
            if (workspace != null)
            {
                await CleanupWorkspaceSafeAsync(workspace);
            }
        }
    }

    private async Task UpdateAnalysisStageAsync(
        AnalysisEntity analysis,
        AnalysisStatus status,
        AnalysisStage stage,
        CancellationToken cancellationToken)
    {
        analysis.Status = status;
        analysis.CurrentStage = stage.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkAnalysisFailedAsync(
        AnalysisEntity analysis,
        AnalysisStage stage,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        analysis.Status = AnalysisStatus.Failed;
        analysis.CurrentStage = stage.ToString();
        analysis.Error = errorMessage;
        analysis.CompletedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistScanResultsAsync(
        AnalysisEntity analysis,
        Application.Models.Scanning.ScanResult scanResult,
        CancellationToken cancellationToken)
    {
        // Add SourceFiles to DbContext
        foreach (var file in scanResult.Files)
        {
            var sourceFile = new SourceFile
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysis.Id,
                Path = file.RelativePath,
                Language = file.Language,
                Size = file.Size,
                Hash = file.Hash,
                AnalysisStatus = FileAnalysisStatus.Pending
            };
            _dbContext.SourceFiles.Add(sourceFile);
        }

        // Add Projects to DbContext
        foreach (var proj in scanResult.DetectedProjects)
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysis.Id,
                Name = proj.Name,
                Path = proj.RelativePath,
                ProjectType = proj.ProjectType.ToString()
            };
            _dbContext.Projects.Add(project);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CleanupWorkspaceSafeAsync(ITemporaryWorkspace workspace)
    {
        try
        {
            await _workspaceManager.CleanupWorkspaceAsync(workspace.AnalysisId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up workspace for analysis {AnalysisId}", workspace.AnalysisId);
            try
            {
                await workspace.CleanupAsync();
            }
            catch
            {
                // ignore secondary cleanup exception
            }
        }
    }
}
