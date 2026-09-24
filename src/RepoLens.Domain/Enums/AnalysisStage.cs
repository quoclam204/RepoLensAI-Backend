namespace RepoLens.Domain.Enums;

/// <summary>
/// Fine-grained processing stages of an analysis pipeline execution.
/// </summary>
public enum AnalysisStage
{
    Validation = 1,
    RepositoryAcquisition = 2,
    FileScanning = 3,
    LanguageDetection = 4,
    ProjectDetection = 5,
    StaticAnalysis = 6,
    DependencyAnalysis = 7,
    ApiAnalysis = 8,
    DatabaseAnalysis = 9,
    EvidenceGeneration = 10,
    Persistence = 11,
    Chunking = 12,
    Embedding = 13,
    Indexing = 14,
    Completed = 15
}
