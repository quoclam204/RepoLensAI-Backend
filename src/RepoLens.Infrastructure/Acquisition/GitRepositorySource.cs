using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Domain.Enums;

namespace RepoLens.Infrastructure.Acquisition;

/// <summary>
/// Repository acquisition source for public Git repository URLs (T028, FR-001).
/// Performs a shallow clone (--depth 1 --no-tags) into the isolated temporary workspace.
/// Strictly prohibits executing any repository scripts or hooks.
/// </summary>
public sealed class GitRepositorySource : IRepositorySource
{
    private readonly AcquisitionOptions _options;
    private readonly ILogger<GitRepositorySource> _logger;

    public GitRepositorySource(
        IOptions<AcquisitionOptions> options,
        ILogger<GitRepositorySource> logger)
    {
        _options = options?.Value ?? new AcquisitionOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public RepositorySourceType SourceType => RepositorySourceType.GitUrl;

    /// <inheritdoc />
    public bool CanHandle(RepositorySourceRequest request)
    {
        return request.Type == RepositorySourceType.GitUrl && !string.IsNullOrWhiteSpace(request.Url);
    }

    /// <inheritdoc />
    public async Task<RepositoryAcquisitionResult> AcquireAsync(
        RepositorySourceRequest request,
        ITemporaryWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(workspace);

        var (isValid, errorMessage) = RepositoryValidator.ValidateGitUrl(request.Url);
        if (!isValid)
        {
            _logger.LogWarning("Git URL validation failed for analysis {AnalysisId}: {Error}", workspace.AnalysisId, errorMessage);
            return RepositoryAcquisitionResult.Failure(errorMessage!);
        }

        var targetPath = Path.GetFullPath(workspace.RootPath);
        Directory.CreateDirectory(targetPath);

        _logger.LogInformation(
            "Cloning Git repository {Url} into workspace {Path} for analysis {AnalysisId}",
            request.Url, targetPath, workspace.AnalysisId);

        try
        {
            var cloneResult = await ExecuteGitCloneAsync(request.Url!.Trim(), targetPath, cancellationToken);
            if (!cloneResult.Success)
            {
                return RepositoryAcquisitionResult.Failure(cloneResult.ErrorMessage ?? "Git clone failed.");
            }

            // Calculate total acquired files and bytes
            var (fileCount, totalBytes) = CalculateDirectoryStats(targetPath);

            // Verify file count limit
            if (fileCount > _options.MaxFileCount)
            {
                var msg = $"Cloned repository exceeds maximum allowed file count of {_options.MaxFileCount} (found {fileCount}).";
                _logger.LogWarning(msg);
                return RepositoryAcquisitionResult.Failure(msg);
            }

            _logger.LogInformation(
                "Successfully cloned repository {Url}: {FileCount} files, {TotalBytes} bytes",
                request.Url, fileCount, totalBytes);

            return new RepositoryAcquisitionResult(
                Success: true,
                TargetPath: targetPath,
                TotalBytes: totalBytes,
                FileCount: fileCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Git clone canceled for analysis {AnalysisId}", workspace.AnalysisId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error cloning Git repository {Url}", request.Url);
            return RepositoryAcquisitionResult.Failure($"Failed to clone repository: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> ExecuteGitCloneAsync(
        string url,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to prevent argument/command injection
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--depth");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("--no-tags");
        startInfo.ArgumentList.Add("--single-branch");
        startInfo.ArgumentList.Add(url);
        startInfo.ArgumentList.Add(targetDirectory);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return (false, "Failed to start Git process. Ensure git is installed and in system PATH.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Git command execution failed: {ex.Message}. Ensure git is installed.");
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.GitTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);
            var errorOutput = await errorTask;

            if (process.ExitCode != 0)
            {
                var cleanError = SanitizeGitErrorMessage(errorOutput);
                _logger.LogWarning("Git clone exited with code {Code}: {Error}", process.ExitCode, cleanError);
                return (false, $"Git clone failed: {cleanError}");
            }

            return (true, null);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return (false, $"Git clone operation timed out after {_options.GitTimeoutSeconds} seconds.");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }
    }

    private static (int FileCount, long TotalBytes) CalculateDirectoryStats(string rootPath)
    {
        int fileCount = 0;
        long totalBytes = 0;

        var directoryInfo = new DirectoryInfo(rootPath);
        if (!directoryInfo.Exists)
        {
            return (0, 0);
        }

        foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            fileCount++;
            totalBytes += file.Length;
        }

        return (fileCount, totalBytes);
    }

    private static string SanitizeGitErrorMessage(string rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
        {
            return "Unknown Git error.";
        }

        // Clean up common git output lines
        var lines = rawError.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("Cloning into"));

        var joined = string.Join("; ", lines);
        return string.IsNullOrWhiteSpace(joined) ? "Git clone failed without error message." : joined;
    }
}
