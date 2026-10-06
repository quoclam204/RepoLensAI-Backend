using RepoLens.Domain.Enums;

namespace RepoLens.Application.Models.Classification;

/// <summary>
/// Result of repository type detection. Contains the detected type, languages,
/// overall confidence, and the evidence chain that led to the classification.
/// </summary>
public sealed class RepositoryClassification
{
    /// <summary>Detected repository type.</summary>
    public RepositoryType Type { get; init; }

    /// <summary>Programming languages detected in the repository (e.g., "C#", "TypeScript").</summary>
    public IReadOnlyList<string> DetectedLanguages { get; init; } = [];

    /// <summary>Overall confidence in the classification based on evidence layers.</summary>
    public DetectionConfidence Confidence { get; init; }

    /// <summary>Individual evidence items that support the classification.</summary>
    public IReadOnlyList<ClassificationEvidence> Evidences { get; init; } = [];

    /// <summary>Human-readable summary of why this type was chosen.</summary>
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// A single piece of evidence supporting the repository type classification.
/// Traces back to a specific file path and reason.
/// </summary>
public sealed class ClassificationEvidence
{
    /// <summary>Relative file path within the repository where evidence was found.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Human-readable description of what was detected and why it matters.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Which detection layer produced this evidence (1=FileMarker, 2=CodePattern, 3=Relationship).</summary>
    public int Layer { get; init; }
}
