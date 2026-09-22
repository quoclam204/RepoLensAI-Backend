namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a detected database entity/table/model (T022).
/// </summary>
public class DatabaseEntity
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? SourceSymbolId { get; set; }

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public CodeSymbol? SourceSymbol { get; set; }

    #endregion
}
