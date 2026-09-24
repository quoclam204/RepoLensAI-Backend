using Microsoft.Extensions.Logging;
using RepoLens.Analysis.Orchestration;
using RepoLens.Analysis.Scanning;
using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Persistence;

namespace RepoLens.Infrastructure.Adapters.Analysis;

/// <summary>
/// Infrastructure adapter implementing Application's IRepositoryAnalyzer abstraction
/// using Roslyn C# AST analysis, TypeScript heuristics, and knowledge graph orchestration.
/// </summary>
public class RoslynRepositoryAnalyzerAdapter : IRepositoryAnalyzer
{
    private readonly RepositoryAnalysisEngine _engine;
    private readonly ILogger<RoslynRepositoryAnalyzerAdapter> _logger;
    private readonly AnalysisLimits _limits;

    public RoslynRepositoryAnalyzerAdapter(
        ILogger<RoslynRepositoryAnalyzerAdapter> logger,
        RepositoryAnalysisEngine? engine = null,
        AnalysisLimits? limits = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _engine = engine ?? new RepositoryAnalysisEngine();
        _limits = limits ?? AnalysisLimits.Default;
    }

    public async Task<AnalysisResultModel> AnalyzeAsync(
        string repositoryPath,
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        _logger.LogInformation("Starting static analysis adapter for AnalysisId {AnalysisId} at {Path}", analysisId, repositoryPath);

        // Untrusted input: Never execute target repo code, shell, or scripts
        var analysisResult = await _engine.AnalyzeRepositoryAsync(
            repositoryRootPath: repositoryPath,
            analysisJobId: analysisId,
            limits: _limits,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Static analysis completed for AnalysisId {AnalysisId}: {NodeCount} nodes, {RelCount} relationships, {ErrorCount} non-fatal errors.",
            analysisId, analysisResult.Analysis.Nodes.Count, analysisResult.Analysis.Relationships.Count, analysisResult.AllErrors.Count);

        // Map to persistence model
        var persistenceModel = AnalysisResultMapper.Map(analysisResult, analysisId);

        return persistenceModel;
    }
}
