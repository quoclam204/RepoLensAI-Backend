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

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    #endregion
}
