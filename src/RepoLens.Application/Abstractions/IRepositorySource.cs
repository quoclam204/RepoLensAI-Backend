using RepoLens.Domain.Enums;

namespace RepoLens.Application.Abstractions;

/// <summary>
/// Abstraction for repository acquisition sources (T027).
/// Each implementation handles a specific source type (Git URL, ZIP upload, etc.).
/// Infrastructure layer provides concrete implementations.
/// </summary>
public interface IRepositorySource
{
    /// <summary>
    /// The source type this implementation handles.
    /// </summary>
    RepositorySourceType SourceType { get; }

    /// <summary>
    /// Determines whether this source can handle the given request.
    /// </summary>
    bool CanHandle(RepositorySourceRequest request);

    /// <summary>
    /// Acquires the repository content into the provided workspace directory.
    /// </summary>
    /// <param name="request">The acquisition request details.</param>
    /// <param name="workspace">The isolated temporary workspace to write files into.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure and basic statistics.</returns>
    Task<RepositoryAcquisitionResult> AcquireAsync(
        RepositorySourceRequest request,
        ITemporaryWorkspace workspace,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request model for repository acquisition.
/// </summary>
/// <param name="Type">The type of source (Git URL or ZIP upload).</param>
/// <param name="Url">Git repository URL. Required when Type is GitUrl.</param>
/// <param name="ContentStream">Stream of uploaded ZIP content. Required when Type is ZipUpload.</param>
/// <param name="FileName">Original file name for ZIP uploads.</param>
/// <param name="ContentLength">Size of the uploaded content in bytes.</param>
public record RepositorySourceRequest(
    RepositorySourceType Type,
    string? Url = null,
    Stream? ContentStream = null,
    string? FileName = null,
    long? ContentLength = null);

/// <summary>
/// Result of a repository acquisition operation.
/// </summary>
/// <param name="Success">Whether the acquisition completed successfully.</param>
/// <param name="TargetPath">Absolute path where the repository content was placed.</param>
/// <param name="TotalBytes">Total size of acquired content in bytes.</param>
/// <param name="FileCount">Number of files acquired.</param>
/// <param name="ErrorMessage">Error description if acquisition failed.</param>
public record RepositoryAcquisitionResult(
    bool Success,
    string TargetPath,
    long TotalBytes,
    int FileCount,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a failure result with an error message.
    /// </summary>
    public static RepositoryAcquisitionResult Failure(string errorMessage)
        => new(false, string.Empty, 0, 0, errorMessage);
}
