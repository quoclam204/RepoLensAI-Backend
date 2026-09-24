namespace RepoLens.Analysis.Scanning;

/// <summary>
/// Configurable limits to protect the static analysis engine against denial of service,
/// resource exhaustion, or infinite traversal from large/malformed repositories.
/// </summary>
public sealed record AnalysisLimits
{
    /// <summary>
    /// Maximum number of files to process across the repository. Default is 10,000.
    /// </summary>
    public int MaxFiles { get; init; } = 10_000;

    /// <summary>
    /// Maximum allowable size for a single file in bytes. Default is 5 MB.
    /// Files exceeding this size will be recorded as warnings and skipped.
    /// </summary>
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum aggregate size of all source code files in bytes. Default is 500 MB.
    /// </summary>
    public long MaxTotalRepositorySizeBytes { get; init; } = 500 * 1024 * 1024;

    /// <summary>
    /// Maximum number of graph relationships allowed before capping further extraction. Default is 50,000.
    /// </summary>
    public int MaxRelationships { get; init; } = 50_000;

    public static AnalysisLimits Default => new();
}
