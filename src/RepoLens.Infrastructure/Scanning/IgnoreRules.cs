namespace RepoLens.Infrastructure.Scanning;

/// <summary>
/// Ignore rules for repository scanning (T033, FR-003, NFR-SEC-001).
/// Filters out non-source directories, build artifacts, external dependencies,
/// media files, binaries, and temporary editor metadata.
/// </summary>
public sealed class IgnoreRules
{
    private static readonly HashSet<string> DefaultIgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".svn",
        ".hg",
        ".vs",
        ".vscode",
        ".idea",
        "bin",
        "obj",
        "node_modules",
        "dist",
        "build",
        "out",
        "coverage",
        "packages",
        "__pycache__",
        ".pytest_cache",
        ".next",
        ".nuxt",
        "target",
        "vendor",
        ".terraform"
    };

    private static readonly HashSet<string> DefaultIgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Executables & Libraries
        ".exe", ".dll", ".so", ".dylib", ".pdb", ".class", ".jar", ".war", ".wasm",
        // Archives
        ".zip", ".tar", ".gz", ".tgz", ".bz2", ".7z", ".rar",
        // Media
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".bmp", ".webp",
        ".mp3", ".mp4", ".wav", ".avi", ".mov", ".mkv",
        // Documents
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        // Compiled / minified / sourcemap
        ".min.js", ".min.css", ".map", ".pyc", ".pyo"
    };

    private readonly HashSet<string> _ignoredDirectories;
    private readonly HashSet<string> _ignoredExtensions;

    public IgnoreRules(ScanningOptions? options = null)
    {
        _ignoredDirectories = new HashSet<string>(DefaultIgnoredDirectories, StringComparer.OrdinalIgnoreCase);
        _ignoredExtensions = new HashSet<string>(DefaultIgnoredExtensions, StringComparer.OrdinalIgnoreCase);

        if (options?.AdditionalIgnoredDirectories != null)
        {
            foreach (var dir in options.AdditionalIgnoredDirectories)
            {
                _ignoredDirectories.Add(dir);
            }
        }

        if (options?.AdditionalIgnoredExtensions != null)
        {
            foreach (var ext in options.AdditionalIgnoredExtensions)
            {
                _ignoredExtensions.Add(ext.StartsWith('.') ? ext : $".{ext}");
            }
        }
    }

    /// <summary>
    /// Determines whether a directory should be skipped during recursive traversal.
    /// </summary>
    public bool ShouldIgnoreDirectory(string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return true;
        }

        return _ignoredDirectories.Contains(directoryName);
    }

    /// <summary>
    /// Determines whether a file should be ignored based on its name or extension.
    /// </summary>
    public bool ShouldIgnoreFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) && _ignoredExtensions.Contains(extension))
        {
            return true;
        }

        // Special check for compound extensions like .min.js or .min.css
        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".js.map", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".css.map", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
