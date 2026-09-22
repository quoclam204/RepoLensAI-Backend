using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Root data transfer model containing normalized knowledge model results from static analysis pipeline (T059).
/// </summary>
public class AnalysisResultModel
{
    public required Guid AnalysisId { get; set; }

    public string? CurrentStage { get; set; }

    public AnalysisStatus? NewStatus { get; set; }

    public string? CommitHash { get; set; }

    public IReadOnlyList<ProjectPersistenceModel> Projects { get; set; } = [];

    public IReadOnlyList<SourceFilePersistenceModel> SourceFiles { get; set; } = [];

    public IReadOnlyList<CodeSymbolPersistenceModel> CodeSymbols { get; set; } = [];

    public IReadOnlyList<EvidencePersistenceModel> Evidences { get; set; } = [];

    public IReadOnlyList<DependencyPersistenceModel> Dependencies { get; set; } = [];

    public IReadOnlyList<ApiEndpointPersistenceModel> ApiEndpoints { get; set; } = [];

    public IReadOnlyList<DatabaseEntityPersistenceModel> DatabaseEntities { get; set; } = [];

    public IReadOnlyList<DatabaseRelationshipPersistenceModel> DatabaseRelationships { get; set; } = [];

    public IReadOnlyList<AnalysisIssuePersistenceModel> Issues { get; set; } = [];

    public IReadOnlyList<DocumentChunkPersistenceModel> DocumentChunks { get; set; } = [];
}
