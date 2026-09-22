namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a detected project (T059).
/// </summary>
public record ProjectPersistenceModel(
    Guid? Id,
    string Name,
    string Path,
    string Language,
    string ProjectType);
