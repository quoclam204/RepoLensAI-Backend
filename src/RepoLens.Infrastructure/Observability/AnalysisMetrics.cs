using System.Diagnostics.Metrics;
using RepoLens.Application.Abstractions;

namespace RepoLens.Infrastructure.Observability;

/// <summary>
/// Implements application analysis and AI metrics using System.Diagnostics.Metrics (T112).
/// Ready for OpenTelemetry / Prometheus export.
/// </summary>
public sealed class AnalysisMetrics : IAnalysisMetrics
{
    public const string MeterName = "RepoLens.Observability";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private readonly Counter<long> _filesScannedCounter = Meter.CreateCounter<long>(
        "repolens.files.scanned", "files", "Total number of repository files scanned");

    private readonly Counter<long> _filesSkippedCounter = Meter.CreateCounter<long>(
        "repolens.files.skipped", "files", "Total number of ignored or skipped files");

    private readonly Counter<long> _symbolsExtractedCounter = Meter.CreateCounter<long>(
        "repolens.symbols.extracted", "symbols", "Total symbols extracted by static analysis");

    private readonly Counter<long> _dependenciesDetectedCounter = Meter.CreateCounter<long>(
        "repolens.dependencies.detected", "dependencies", "Total code dependencies detected");

    private readonly Counter<long> _endpointsDetectedCounter = Meter.CreateCounter<long>(
        "repolens.endpoints.detected", "endpoints", "Total API endpoints detected");

    private readonly Counter<long> _databaseEntitiesCounter = Meter.CreateCounter<long>(
        "repolens.database_entities.detected", "entities", "Total database entities detected");

    private readonly Histogram<double> _analysisDurationHistogram = Meter.CreateHistogram<double>(
        "repolens.analysis.duration_ms", "ms", "Duration of full static analysis in milliseconds");

    private readonly Histogram<double> _embeddingDurationHistogram = Meter.CreateHistogram<double>(
        "repolens.embedding.duration_ms", "ms", "Duration of chunk embedding generation in milliseconds");

    private readonly Counter<long> _embeddingsGeneratedCounter = Meter.CreateCounter<long>(
        "repolens.embeddings.generated", "embeddings", "Total document chunk vector embeddings generated");

    private readonly Histogram<double> _ragLatencyHistogram = Meter.CreateHistogram<double>(
        "repolens.rag.latency_ms", "ms", "Total latency of RAG query answering in milliseconds");

    private readonly Counter<long> _aiRequestsCounter = Meter.CreateCounter<long>(
        "repolens.ai.requests", "requests", "Total AI provider requests executed");

    public void RecordFilesScanned(int count) => _filesScannedCounter.Add(count);
    public void RecordFilesSkipped(int count) => _filesSkippedCounter.Add(count);
    public void RecordSymbolsExtracted(int count) => _symbolsExtractedCounter.Add(count);
    public void RecordDependenciesDetected(int count) => _dependenciesDetectedCounter.Add(count);
    public void RecordEndpointsDetected(int count) => _endpointsDetectedCounter.Add(count);
    public void RecordDatabaseEntitiesDetected(int count) => _databaseEntitiesCounter.Add(count);
    public void RecordAnalysisDuration(double milliseconds) => _analysisDurationHistogram.Record(milliseconds);
    public void RecordEmbeddingDuration(double milliseconds) => _embeddingDurationHistogram.Record(milliseconds);
    public void RecordEmbeddingsGenerated(int count) => _embeddingsGeneratedCounter.Add(count);
    public void RecordRagLatency(double milliseconds) => _ragLatencyHistogram.Record(milliseconds);

    public void RecordAiRequest(string provider, bool success) =>
        _aiRequestsCounter.Add(1, new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("success", success));
}
