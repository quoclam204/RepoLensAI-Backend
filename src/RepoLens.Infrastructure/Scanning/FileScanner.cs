using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Models.Scanning;

namespace RepoLens.Infrastructure.Scanning;

/// <summary>
/// Implementation of repository file scanner (T032, FR-003).
/// Scans the repository tree, enforces ignore rules, detects secrets,
/// identifies languages and projects, and calculates SHA-256 file hashes.
/// Produces <see cref="ScanResult"/> as input for Person 2 (Static Analysis).
/// </summary>
public sealed class FileScanner : IScannerService
{
    private readonly ScanningOptions _options;
    private readonly IgnoreRules _ignoreRules;
    private readonly SecretDetector _secretDetector;
    private readonly LanguageDetector _languageDetector;
    private readonly ProjectDetector _projectDetector;
    private readonly ILogger<FileScanner> _logger;

    public FileScanner(
        IOptions<ScanningOptions> options,
        IgnoreRules ignoreRules,
        SecretDetector secretDetector,
        LanguageDetector languageDetector,
        ProjectDetector projectDetector,
        ILogger<FileScanner> logger)
    {
        _options = options?.Value ?? new ScanningOptions();
        _ignoreRules = ignoreRules ?? new IgnoreRules();
        _secretDetector = secretDetector ?? new SecretDetector();
        _languageDetector = languageDetector ?? new LanguageDetector();
        _projectDetector = projectDetector ?? new ProjectDetector();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ScanResult> ScanAsync(
        Guid analysisId,
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!Directory.Exists(workspaceRoot))
        {
            throw new DirectoryNotFoundException($"Workspace directory does not exist: {workspaceRoot}");
        }

        var normalizedRoot = Path.GetFullPath(workspaceRoot);
        _logger.LogInformation("Beginning scan for analysis {AnalysisId} at {Path}", analysisId, normalizedRoot);

        var stopwatch = Stopwatch.StartNew();

        var discoveredFiles = new List<ScannedFile>();
        var discoveredProjects = new List<ScannedProject>();
        var detectedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rootDirInfo = new DirectoryInfo(normalizedRoot);
        await TraverseDirectoryAsync(
            rootDirInfo,
            normalizedRoot,
            discoveredFiles,
            discoveredProjects,
            detectedLanguages,
            cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Scan completed for analysis {AnalysisId}: {Count} files, {ProjCount} projects in {Elapsed}ms",
            analysisId, discoveredFiles.Count, discoveredProjects.Count, stopwatch.ElapsedMilliseconds);

        return new ScanResult
        {
            AnalysisId = analysisId,
            WorkspaceRoot = normalizedRoot,
            Files = discoveredFiles,
            DetectedLanguages = detectedLanguages.OrderBy(l => l).ToList(),
            DetectedProjects = discoveredProjects,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task TraverseDirectoryAsync(
        DirectoryInfo currentDir,
        string rootPath,
        List<ScannedFile> files,
        List<ScannedProject> projects,
        HashSet<string> languages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Process files in current directory
        FileInfo[] fileInfos;
        try
        {
            fileInfos = currentDir.GetFiles();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Access denied reading directory: {Path} ({Message})", currentDir.FullName, ex.Message);
            return;
        }

        foreach (var fileInfo in fileInfos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = fileInfo.Name;

            // Check ignore rules (T033)
            if (_ignoreRules.ShouldIgnoreFile(fileName))
            {
                continue;
            }

            // Check secret detection boundary (T034, NFR-SEC-001)
            if (_secretDetector.IsSecretFile(fileName) || await _secretDetector.ContainsSecretContentAsync(fileInfo.FullName, cancellationToken))
            {
                _logger.LogInformation("Excluded sensitive secret file from analysis scan: {File}", fileName);
                continue;
            }

            // Check file size limit
            if (fileInfo.Length > _options.MaxScannedFileBytes)
            {
                _logger.LogWarning("File exceeds maximum scan size: {File} ({Size} bytes)", fileName, fileInfo.Length);
                continue;
            }

            // Calculate relative path normalized with forward slashes
            var relativePath = Path.GetRelativePath(rootPath, fileInfo.FullName)
                .Replace('\\', '/');

            // Detect language (T035)
            var language = _languageDetector.DetectLanguage(fileInfo.FullName);
            if (language != "Unknown")
            {
                languages.Add(language);
            }

            // Calculate SHA-256 hash (T032)
            var hash = await ComputeSha256HashAsync(fileInfo.FullName, cancellationToken);

            var scannedFile = new ScannedFile(
                RelativePath: relativePath,
                Extension: fileInfo.Extension,
                Size: fileInfo.Length,
                Hash: hash,
                Language: language);

            files.Add(scannedFile);

            // Detect project (T036)
            var project = _projectDetector.DetectProject(relativePath, fileInfo.FullName);
            if (project != null)
            {
                projects.Add(project);
            }
        }

        // 2. Recurse into subdirectories
        DirectoryInfo[] subDirs;
        try
        {
            subDirs = currentDir.GetDirectories();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Access denied reading subdirectories of: {Path} ({Message})", currentDir.FullName, ex.Message);
            return;
        }

        foreach (var subDir in subDirs)
        {
            if (_ignoreRules.ShouldIgnoreDirectory(subDir.Name))
            {
                continue;
            }

            await TraverseDirectoryAsync(subDir, rootPath, files, projects, languages, cancellationToken);
        }
    }

    private static async Task<string> ComputeSha256HashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
