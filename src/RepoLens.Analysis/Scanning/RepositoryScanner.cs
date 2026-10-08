namespace RepoLens.Analysis.Scanning;

public sealed record ScannedFile(
    string RelativePath,
    string FullPath,
    string Extension,
    long SizeInBytes,
    string Category,
    string Hash = "");

public sealed record ScannedProject(
    string ProjectName,
    string RelativePath,
    string FullPath,
    string ProjectType);

public sealed record ScannedRepository(
    string RootPath,
    IReadOnlyList<ScannedProject> Projects,
    IReadOnlyList<ScannedFile> SourceFiles,
    IReadOnlyList<ScannedFile> ConfigurationFiles,
    IReadOnlyList<string> SolutionFiles,
    IReadOnlyList<string> ScanErrors);

/// <summary>
/// Safe, deterministic repository directory scanner that discovers solutions, projects, source files,
/// and configurations while strictly enforcing ignore rules and path traversal protection.
/// </summary>
public class RepositoryScanner
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "bin", "obj", "node_modules", "dist", "build",
        "coverage", "out", "target", ".next", ".nuxt", ".output", "tmp", "temp", "artifacts",
        "secrets", ".secrets"
    };

    private static readonly HashSet<string> CSharpExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs"
    };

    private static readonly HashSet<string> RazorExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cshtml", ".razor"
    };

    private static readonly HashSet<string> WebAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".scss", ".sass", ".less"
    };

    private static readonly HashSet<string> TypeScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".tsx"
    };

    private static readonly HashSet<string> JavaScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".jsx", ".mjs", ".cjs"
    };

    private static readonly HashSet<string> JavaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".java", ".kt", ".kts"
    };

    private static readonly HashSet<string> PythonExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".py"
    };

    private static readonly HashSet<string> GoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".go"
    };

    private static readonly HashSet<string> PhpExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".php"
    };

    private static readonly HashSet<string> RustExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rs"
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yaml", ".yml", ".xml", ".config"
    };

    private static readonly HashSet<string> DocumentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".rst", ".adoc"
    };

    public ScannedRepository Scan(
        string repositoryRootPath,
        AnalysisLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);

        var effectiveLimits = limits ?? AnalysisLimits.Default;

        if (!Directory.Exists(repositoryRootPath))
        {
            return new ScannedRepository(
                RootPath: repositoryRootPath,
                Projects: [],
                SourceFiles: [],
                ConfigurationFiles: [],
                SolutionFiles: [],
                ScanErrors: [$"Directory not found: {repositoryRootPath}"]);
        }

        var rootFullPath = Path.GetFullPath(repositoryRootPath);
        var projects = new List<ScannedProject>();
        var sourceFiles = new List<ScannedFile>();
        var configFiles = new List<ScannedFile>();
        var solutionFiles = new List<string>();
        var scanErrors = new List<string>();
        long currentTotalBytes = 0;

        try
        {
            ScanDirectoryRecursive(
                currentDir: rootFullPath,
                rootFullPath: rootFullPath,
                projects: projects,
                sourceFiles: sourceFiles,
                configFiles: configFiles,
                solutionFiles: solutionFiles,
                scanErrors: scanErrors,
                limits: effectiveLimits,
                refCurrentTotalBytes: ref currentTotalBytes,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            scanErrors.Add("Repository scan was cancelled.");
        }
        catch (Exception ex)
        {
            scanErrors.Add($"Fatal directory traversal error: {ex.Message}");
        }

        // Deterministic ordering by relative path
        var sortedProjects = projects.OrderBy(p => p.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        var sortedSourceFiles = sourceFiles.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        var sortedConfigFiles = configFiles.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        var sortedSolutions = solutionFiles.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

        return new ScannedRepository(
            RootPath: rootFullPath.Replace('\\', '/'),
            Projects: sortedProjects.AsReadOnly(),
            SourceFiles: sortedSourceFiles.AsReadOnly(),
            ConfigurationFiles: sortedConfigFiles.AsReadOnly(),
            SolutionFiles: sortedSolutions.AsReadOnly(),
            ScanErrors: scanErrors.AsReadOnly());
    }

    private static void ScanDirectoryRecursive(
        string currentDir,
        string rootFullPath,
        List<ScannedProject> projects,
        List<ScannedFile> sourceFiles,
        List<ScannedFile> configFiles,
        List<string> solutionFiles,
        List<string> scanErrors,
        AnalysisLimits limits,
        ref long refCurrentTotalBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        // Path traversal safety check on current directory
        if (!currentDir.Equals(rootFullPath, StringComparison.OrdinalIgnoreCase) &&
            !currentDir.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            scanErrors.Add($"Path traversal attempt detected: {currentDir}");
            return;
        }

        var dirRelative = Path.GetRelativePath(rootFullPath, currentDir).Replace('\\', '/');
        if (dirRelative.StartsWith("..") || Path.IsPathRooted(dirRelative))
        {
            scanErrors.Add($"Path traversal attempt detected: {currentDir}");
            return;
        }

        DirectoryInfo dirInfo;
        try
        {
            dirInfo = new DirectoryInfo(currentDir);
            if (!dirInfo.Exists) return;
        }
        catch (Exception ex)
        {
            scanErrors.Add($"Could not access directory '{currentDir}': {ex.Message}");
            return;
        }

        // Process files in current directory
        try
        {
            foreach (var file in dirInfo.EnumerateFiles())
            {
                var fileFullPath = file.FullName;

                // Path traversal safety check
                if (!fileFullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    scanErrors.Add($"Path traversal attempt detected: {fileFullPath}");
                    continue;
                }

                var relativePath = Path.GetRelativePath(rootFullPath, fileFullPath).Replace('\\', '/');
                if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath))
                {
                    scanErrors.Add($"Path traversal attempt detected: {fileFullPath}");
                    continue;
                }

                var ext = file.Extension;
                var fileName = file.Name;

                // Check file limits
                if (sourceFiles.Count >= limits.MaxFiles)
                {
                    scanErrors.Add($"Maximum file count limit reached ({limits.MaxFiles}). Traversal stopped.");
                    return;
                }

                if (file.Length > limits.MaxFileSizeBytes)
                {
                    scanErrors.Add($"Skipping excessively large file '{relativePath}' ({file.Length} bytes exceeds limit of {limits.MaxFileSizeBytes} bytes)");
                    continue;
                }

                if (refCurrentTotalBytes + file.Length > limits.MaxTotalRepositorySizeBytes)
                {
                    scanErrors.Add($"Maximum repository total size limit reached ({limits.MaxTotalRepositorySizeBytes} bytes). Traversal stopped.");
                    return;
                }

                refCurrentTotalBytes += file.Length;

                // Check solutions
                if (ext.Equals(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    solutionFiles.Add(relativePath);
                    continue;
                }

                // Check C# project manifests
                if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    projects.Add(new ScannedProject(
                        ProjectName: Path.GetFileNameWithoutExtension(fileName),
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "CSharp"));
                    continue;
                }

                // Check Node / npm package manifests
                if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(fileFullPath)) ?? "root";
                    projects.Add(new ScannedProject(
                        ProjectName: parentDirName,
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "Node"));

                    var pkgHash = ComputeFileHash(fileFullPath);
                    configFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Configuration",
                        Hash: pkgHash));
                    continue;
                }

                // Check Java Maven / Gradle manifests
                if (fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(fileFullPath)) ?? "root";
                    projects.Add(new ScannedProject(
                        ProjectName: parentDirName,
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "Java"));
                }

                // Check Python manifests
                if (fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Pipfile", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(fileFullPath)) ?? "root";
                    projects.Add(new ScannedProject(
                        ProjectName: parentDirName,
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "Python"));
                }

                // Check Go manifest
                if (fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(fileFullPath)) ?? "root";
                    projects.Add(new ScannedProject(
                        ProjectName: parentDirName,
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "Go"));
                }

                // Check PHP Composer manifest
                if (fileName.Equals("composer.json", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(fileFullPath)) ?? "root";
                    projects.Add(new ScannedProject(
                        ProjectName: parentDirName,
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "PHP"));
                }

                // Check Rust Cargo manifest
                if (fileName.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDirName = Path.GetFileName(Path.GetDirectoryName(fileFullPath)) ?? "root";
                    projects.Add(new ScannedProject(
                        ProjectName: parentDirName,
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        ProjectType: "Rust"));
                }

                // Check generated and secret/env files to ignore (T033)
                if (IsIgnoredFile(fileName))
                {
                    continue;
                }

                var fileHash = ComputeFileHash(fileFullPath);

                // Categorize source files
                if (CSharpExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "CSharp",
                        Hash: fileHash));
                }
                else if (RazorExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Razor",
                        Hash: fileHash));
                }
                else if (WebAssetExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "WebAsset",
                        Hash: fileHash));
                }
                else if (TypeScriptExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "TypeScript",
                        Hash: fileHash));
                }
                else if (JavaScriptExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "JavaScript",
                        Hash: fileHash));
                }
                else if (JavaExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Java",
                        Hash: fileHash));
                }
                else if (PythonExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Python",
                        Hash: fileHash));
                }
                else if (GoExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Go",
                        Hash: fileHash));
                }
                else if (PhpExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "PHP",
                        Hash: fileHash));
                }
                else if (RustExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Rust",
                        Hash: fileHash));
                }
                else if (DocumentationExtensions.Contains(ext))
                {
                    sourceFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Documentation",
                        Hash: fileHash));
                }
                else if (IsRelevantConfigFile(fileName, ext))
                {
                    configFiles.Add(new ScannedFile(
                        RelativePath: relativePath,
                        FullPath: fileFullPath.Replace('\\', '/'),
                        Extension: ext,
                        SizeInBytes: file.Length,
                        Category: "Configuration",
                        Hash: fileHash));
                }
            }
        }
        catch (Exception ex)
        {
            scanErrors.Add($"Error reading files in '{currentDir}': {ex.Message}");
        }

        // Recurse subdirectories
        try
        {
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IgnoredDirectories.Contains(subDir.Name))
                {
                    continue;
                }

                // Skip hidden directories (starting with '.')
                if (subDir.Name.StartsWith('.') && !subDir.Name.Equals(".config", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ScanDirectoryRecursive(
                    currentDir: subDir.FullName,
                    rootFullPath: rootFullPath,
                    projects: projects,
                    sourceFiles: sourceFiles,
                    configFiles: configFiles,
                    solutionFiles: solutionFiles,
                    scanErrors: scanErrors,
                    limits: limits,
                    refCurrentTotalBytes: ref refCurrentTotalBytes,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            scanErrors.Add($"Error accessing subdirectories in '{currentDir}': {ex.Message}");
        }
    }

    private static bool IsIgnoredFile(string fileName)
    {
        if (IsGeneratedFile(fileName))
        {
            return true;
        }

        // Ignore .env and secret files (T033 / NFR-SEC-001)
        if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".secrets.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains(".secret.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsGeneratedFile(string fileName)
    {
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsRelevantConfigFile(string fileName, string ext)
    {
        if (fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.StartsWith("tsconfig", StringComparison.OrdinalIgnoreCase) && ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("NuGet.Config", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
