namespace RepoLens.Application.Abstractions;

/// <summary>
/// Abstraction for tracking analysis and RAG performance and operational metrics (T112).
/// Follows NFR-OBS-001 with zero secret leakage.
/// </summary>
public interface IAnalysisMetrics
{
    void RecordFilesScanned(int count);
    void RecordFilesSkipped(int count);
    void RecordSymbolsExtracted(int count);
    void RecordDependenciesDetected(int count);
    void RecordEndpointsDetected(int count);
    void RecordDatabaseEntitiesDetected(int count);
    void RecordAnalysisDuration(double milliseconds);
    void RecordEmbeddingDuration(double milliseconds);
    void RecordEmbeddingsGenerated(int count);
    void RecordRagLatency(double milliseconds);
    void RecordAiRequest(string provider, bool success);
}
