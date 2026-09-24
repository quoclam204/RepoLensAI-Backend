using RepoLens.Application.DTOs.Persistence;
using RepoLens.Application.Models.Architecture;
using RepoLens.Application.Models.Archify;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Adapter contract to convert RepoLens ArchitectureModel or AnalysisResultModel
/// into an Archify-compatible C4 specification document (docs/ideas/archify.md).
/// </summary>
public interface IArchifyAdapter
{
    /// <summary>
    /// Converts a normalized ArchitectureModel into an Archify document.
    /// </summary>
    ArchifyDocument ConvertToArchify(ArchitectureModel architectureModel);

    /// <summary>
    /// Converts an AnalysisResultModel directly into an Archify document, preserving containers,
    /// components, relationships, and evidence IDs.
    /// </summary>
    ArchifyDocument ConvertFromAnalysis(AnalysisResultModel analysisResult);
}
