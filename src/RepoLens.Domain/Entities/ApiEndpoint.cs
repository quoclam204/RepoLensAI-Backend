namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a detected HTTP endpoint (controller action or minimal API route) (T021).
/// </summary>
public class ApiEndpoint
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public Guid ProjectId { get; set; }

    public string Method { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public string? Controller { get; set; }

    public string? Action { get; set; }

    public Guid? SymbolId { get; set; }

    public Guid? EvidenceId { get; set; }

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public Project Project { get; set; } = null!;

    public CodeSymbol? Symbol { get; set; }

    public Evidence? Evidence { get; set; }

    #endregion
}
