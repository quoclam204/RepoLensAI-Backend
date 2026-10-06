using RepoLens.Application.DTOs.Diagrams;
using RepoLens.Application.Models.Classification;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Service responsible for producing repository-type-specific diagrams and classifications (Giai đoạn 2).
/// Tailors diagram types, nodes, and edges to the detected repository type.
/// </summary>
public interface IDiagramService
{
    /// <summary>
    /// Gets the repository classification for the specified analysis.
    /// </summary>
    Task<RepositoryClassification> GetClassificationAsync(Guid analysisId, CancellationToken ct = default);

    /// <summary>
    /// Generates a diagram for the specified analysis and diagram type.
    /// If diagramType is "default" or empty, returns the primary diagram for the repository type.
    /// </summary>
    Task<DiagramDto> GetDiagramAsync(Guid analysisId, string? diagramType = null, CancellationToken ct = default);
}
