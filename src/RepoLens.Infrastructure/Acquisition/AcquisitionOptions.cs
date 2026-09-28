namespace RepoLens.Infrastructure.Acquisition;

/// <summary>
/// Configuration options for repository acquisition (T028 - T030).
/// </summary>
public class AcquisitionOptions
{
    public const string SectionName = "RepositoryAcquisition";

    /// <summary>
    /// Maximum allowed uncompressed size in bytes for ZIP archives (default: 200 MB).
    /// Protects against decompression bombs (Zip Bombs).
    /// </summary>
    public long MaxUncompressedBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>
    /// Maximum allowed number of files in an archive or repository (default: 10,000).
    /// </summary>
    public int MaxFileCount { get; set; } = 10_000;

    /// <summary>
    /// Maximum allowed size for a single file in bytes (default: 50 MB).
    /// </summary>
    public long MaxSingleFileBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>
    /// Timeout in seconds for Git clone operations (default: 120 seconds).
    /// </summary>
    public int GitTimeoutSeconds { get; set; } = 120;
}
