namespace RepoLens.Infrastructure.Scanning;

/// <summary>
/// Configuration options for repository scanning (T032 - T036).
/// </summary>
public class ScanningOptions
{
    public const string SectionName = "RepositoryScanning";

    /// <summary>
    /// Additional custom directory names to ignore.
    /// </summary>
    public List<string> AdditionalIgnoredDirectories { get; set; } = new();

    /// <summary>
    /// Additional custom file extensions to ignore (including the leading dot).
    /// </summary>
    public List<string> AdditionalIgnoredExtensions { get; set; } = new();

    /// <summary>
    /// Maximum file size to compute hash and include in scan (default: 20 MB).
    /// Files larger than this are excluded to conserve memory and I/O.
    /// </summary>
    public long MaxScannedFileBytes { get; set; } = 20L * 1024 * 1024;
}
