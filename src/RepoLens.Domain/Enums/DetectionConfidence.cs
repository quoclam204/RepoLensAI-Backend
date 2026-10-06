namespace RepoLens.Domain.Enums;

/// <summary>
/// Confidence level for repository type detection.
/// Separate from <see cref="RepoLens.Domain.ValueObjects.ConfidenceScore"/> which is a continuous 0-1 float
/// used for individual evidence items. This enum represents the overall classification confidence.
/// </summary>
public enum DetectionConfidence
{
    /// <summary>Unable to determine type.</summary>
    Unknown = 0,

    /// <summary>Weak signals only (e.g., only file markers, no code-level confirmation).</summary>
    Low = 1,

    /// <summary>File markers and some code-level evidence agree.</summary>
    Medium = 2,

    /// <summary>Strong evidence across multiple layers (file markers, code patterns, relationships).</summary>
    High = 3
}
