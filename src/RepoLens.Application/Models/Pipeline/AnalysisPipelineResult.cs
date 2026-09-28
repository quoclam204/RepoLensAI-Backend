using RepoLens.Application.Models.Scanning;
using RepoLens.Domain.Enums;

namespace RepoLens.Application.Models.Pipeline;

/// <summary>
/// Result of executing the analysis acquisition and scanning pipeline.
/// </summary>
/// <param name="Success">Whether the pipeline stage executed successfully.</param>
/// <param name="FinalStatus">The analysis status upon completion or failure.</param>
/// <param name="FinalStage">The last stage reached in the lifecycle.</param>
/// <param name="ScanResult">The scan result produced if acquisition and scanning succeeded.</param>
/// <param name="ErrorMessage">Error details if the pipeline failed.</param>
public record AnalysisPipelineResult(
    bool Success,
    AnalysisStatus FinalStatus,
    AnalysisStage FinalStage,
    ScanResult? ScanResult = null,
    string? ErrorMessage = null)
{
    public static AnalysisPipelineResult Succeeded(AnalysisStage finalStage, ScanResult scanResult)
        => new(true, AnalysisStatus.Completed, finalStage, scanResult);

    public static AnalysisPipelineResult Failed(AnalysisStage failedStage, string errorMessage)
        => new(false, AnalysisStatus.Failed, failedStage, null, errorMessage);
}
