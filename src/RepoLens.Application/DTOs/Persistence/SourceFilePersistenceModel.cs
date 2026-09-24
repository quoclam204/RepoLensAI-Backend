using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a source file within a project (T059).
/// </summary>
public record SourceFilePersistenceModel(
    Guid? Id,
    string? ProjectPath,
    Guid? ProjectId,
    string Path,
    string Language,
    long Size,
    string Hash,
    FileAnalysisStatus AnalysisStatus);
