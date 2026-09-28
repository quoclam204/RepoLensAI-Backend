using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions;
using RepoLens.Domain.Enums;

namespace RepoLens.Infrastructure.Acquisition;

/// <summary>
/// Repository acquisition source for uploaded ZIP archives (T029, FR-001, NFR-002).
/// Safely unpacks archives into an isolated temporary workspace while strictly enforcing:
/// 1. Path Traversal (Zip Slip) prevention.
/// 2. Decompression Bomb (Zip Bomb) mitigation via uncompressed size limits.
/// 3. Maximum file count limits.
/// 4. Maximum individual file size limits.
/// </summary>
public sealed class ZipRepositorySource : IRepositorySource
{
    private readonly AcquisitionOptions _options;
    private readonly ILogger<ZipRepositorySource> _logger;

    public ZipRepositorySource(
        IOptions<AcquisitionOptions> options,
        ILogger<ZipRepositorySource> logger)
    {
        _options = options?.Value ?? new AcquisitionOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public RepositorySourceType SourceType => RepositorySourceType.ZipUpload;

    /// <inheritdoc />
    public bool CanHandle(RepositorySourceRequest request)
    {
        return request.Type == RepositorySourceType.ZipUpload && request.ContentStream != null;
    }

    /// <inheritdoc />
    public async Task<RepositoryAcquisitionResult> AcquireAsync(
        RepositorySourceRequest request,
        ITemporaryWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(workspace);

        if (request.ContentStream == null)
        {
            return RepositoryAcquisitionResult.Failure("ZIP content stream is null.");
        }

        var (isValid, errorMessage) = RepositoryValidator.ValidateZipUpload(request, _options);
        if (!isValid)
        {
            _logger.LogWarning("ZIP validation failed for analysis {AnalysisId}: {Error}", workspace.AnalysisId, errorMessage);
            return RepositoryAcquisitionResult.Failure(errorMessage!);
        }

        var normalizedDestination = Path.GetFullPath(workspace.RootPath);
        if (!normalizedDestination.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedDestination += Path.DirectorySeparatorChar;
        }

        Directory.CreateDirectory(normalizedDestination);

        long totalUncompressedBytes = 0;
        int extractedFileCount = 0;

        try
        {
            _logger.LogInformation(
                "Extracting ZIP archive for analysis {AnalysisId} into {Destination}",
                workspace.AnalysisId, normalizedDestination);

            using var archive = new ZipArchive(request.ContentStream, ZipArchiveMode.Read, leaveOpen: true);

            // First pass: validate archive integrity and total file count before extracting
            if (archive.Entries.Count > _options.MaxFileCount)
            {
                var msg = $"ZIP archive exceeds the maximum allowed file count of {_options.MaxFileCount} (found {archive.Entries.Count}).";
                _logger.LogWarning(msg);
                return RepositoryAcquisitionResult.Failure(msg);
            }

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Compute destination path and verify it doesn't escape normalizedDestination (Zip Slip defense)
                var destinationPath = Path.GetFullPath(Path.Combine(normalizedDestination, entry.FullName));
                if (!destinationPath.StartsWith(normalizedDestination, StringComparison.OrdinalIgnoreCase))
                {
                    var msg = $"Path traversal attempt detected in ZIP entry: '{entry.FullName}'. Extraction aborted.";
                    _logger.LogError(msg);
                    return RepositoryAcquisitionResult.Failure(msg);
                }

                // If entry is a directory
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                // Verify single file uncompressed size limit
                if (entry.Length > _options.MaxSingleFileBytes)
                {
                    var msg = $"ZIP entry '{entry.FullName}' exceeds maximum single file size limit ({entry.Length} bytes > {_options.MaxSingleFileBytes} bytes).";
                    _logger.LogWarning(msg);
                    return RepositoryAcquisitionResult.Failure(msg);
                }

                // Verify total cumulative uncompressed size (Zip Bomb defense)
                totalUncompressedBytes += entry.Length;
                if (totalUncompressedBytes > _options.MaxUncompressedBytes)
                {
                    var msg = $"ZIP archive uncompressed size exceeds maximum allowed limit of {_options.MaxUncompressedBytes / (1024 * 1024)} MB (Zip bomb defense triggered).";
                    _logger.LogError(msg);
                    return RepositoryAcquisitionResult.Failure(msg);
                }

                // Ensure parent directory exists
                var parentDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // Extract file content safely with buffer streaming
                await using (var entryStream = entry.Open())
                await using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
                {
                    await entryStream.CopyToAsync(fileStream, cancellationToken);
                }

                extractedFileCount++;
            }

            _logger.LogInformation(
                "Successfully extracted {FileCount} files ({TotalBytes} bytes) for analysis {AnalysisId}",
                extractedFileCount, totalUncompressedBytes, workspace.AnalysisId);

            return new RepositoryAcquisitionResult(
                Success: true,
                TargetPath: normalizedDestination,
                TotalBytes: totalUncompressedBytes,
                FileCount: extractedFileCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ZIP extraction canceled for analysis {AnalysisId}", workspace.AnalysisId);
            throw;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Corrupt or invalid ZIP archive for analysis {AnalysisId}", workspace.AnalysisId);
            return RepositoryAcquisitionResult.Failure($"Corrupt ZIP archive: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting ZIP archive for analysis {AnalysisId}", workspace.AnalysisId);
            return RepositoryAcquisitionResult.Failure($"Failed to extract archive: {ex.Message}");
        }
    }
}
