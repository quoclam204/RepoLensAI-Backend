namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a detected project or module within an analyzed repository (T017).
/// </summary>
public class Project
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string ProjectType { get; set; } = string.Empty;

    #region Navigation Properties

    public Analysis Analysis { get; set; } = null!;

    public ICollection<SourceFile> SourceFiles { get; set; } = new List<SourceFile>();

    public ICollection<ApiEndpoint> ApiEndpoints { get; set; } = new List<ApiEndpoint>();

    #endregion
}
