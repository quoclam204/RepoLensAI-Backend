using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents verified source code evidence grounding an analysis finding or relationship (T023).
/// </summary>
public class Evidence
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string? Symbol { get; set; }

    public int StartLine { get; set; }

    public int EndLine { get; set; }

    public EvidenceType EvidenceType { get; set; }

    public string Description { get; set; } = string.Empty;

    // Compatibility properties for static analysis engine and RAG
    public Guid AnalysisJobId
    {
        get => AnalysisId;
        set => AnalysisId = value;
    }

    public string Snippet
    {
        get => Description;
        set => Description = value;
    }

    public ValueObjects.SourceLocation Location => new(FilePath, StartLine, EndLine);

    public ValueObjects.ConfidenceScore? Confidence { get; set; }

    public static Evidence Create(
        Guid analysisJobId,
        ValueObjects.SourceLocation location,
        string snippet,
        EvidenceType evidenceType,
        ValueObjects.ConfidenceScore? confidence = null,
        string? symbol = null)
    {
        return new Evidence
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisJobId,
            FilePath = location.FilePath,
            StartLine = location.StartLine,
            EndLine = location.EndLine,
            Description = snippet,
            EvidenceType = evidenceType,
            Confidence = confidence,
            Symbol = symbol
        };
    }

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    #endregion
}
