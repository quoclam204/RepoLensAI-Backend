using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting verified source evidence (T059).
/// </summary>
public record EvidencePersistenceModel(
    Guid? Id,
    string? EvidenceKey,
    string FilePath,
    string? Symbol,
    int StartLine,
    int EndLine,
    EvidenceType EvidenceType,
    string Description,
    float? ConfidenceScore = null);
