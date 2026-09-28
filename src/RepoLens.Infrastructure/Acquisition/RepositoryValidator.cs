using System.Text.RegularExpressions;
using RepoLens.Application.Abstractions;
using RepoLens.Domain.Enums;

namespace RepoLens.Infrastructure.Acquisition;

/// <summary>
/// Validator for repository acquisition requests (T030, FR-001, NFR-SEC-001).
/// Validates Git URLs and ZIP file payloads before processing to guarantee untrusted inputs are safe.
/// </summary>
public static class RepositoryValidator
{
    // Regex for safe Git URLs: only http / https protocols, alphanumeric domain and path, optional .git suffix
    private static readonly Regex SafeGitUrlPattern = new(
        @"^https?://([a-zA-Z0-9\-_.]+)((:[0-9]+)?)/([a-zA-Z0-9\-_.%/]+?)(?:\.git)?/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates a repository acquisition request.
    /// </summary>
    /// <param name="request">The acquisition request to validate.</param>
    /// <param name="options">Acquisition options containing resource limits.</param>
    /// <returns>(IsValid, ErrorMessage)</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(RepositorySourceRequest request, AcquisitionOptions options)
    {
        if (request == null)
        {
            return (false, "Request cannot be null.");
        }

        return request.Type switch
        {
            RepositorySourceType.GitUrl => ValidateGitUrl(request.Url),
            RepositorySourceType.ZipUpload => ValidateZipUpload(request, options),
            _ => (false, $"Unsupported repository source type: {request.Type}")
        };
    }

    /// <summary>
    /// Validates a Git URL for security and protocol compliance.
    /// Only public HTTP and HTTPS URLs are accepted in MVP.
    /// Rejects command injection characters and unsupported protocols (e.g. file://, ssh://, git://).
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateGitUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "Git repository URL is required.");
        }

        var trimmedUrl = url.Trim();

        // Check for disallowed characters that might cause argument injection or shell execution
        if (trimmedUrl.Contains(' ') || trimmedUrl.Contains('\n') || trimmedUrl.Contains('\r') ||
            trimmedUrl.Contains(';') || trimmedUrl.Contains('&') || trimmedUrl.Contains('|') ||
            trimmedUrl.Contains('`') || trimmedUrl.Contains('$') || trimmedUrl.StartsWith('-'))
        {
            return (false, "Git URL contains invalid or potentially dangerous characters.");
        }

        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            return (false, "Git URL is not a valid absolute URI.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return (false, $"Unsupported URI scheme '{uri.Scheme}'. Only HTTP and HTTPS are supported in MVP.");
        }

        if (!SafeGitUrlPattern.IsMatch(trimmedUrl))
        {
            return (false, "Git URL format is invalid. Expected format: https://github.com/owner/repo");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates an uploaded ZIP archive before extraction.
    /// Checks content stream availability, size limits, and ZIP header signatures.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateZipUpload(RepositorySourceRequest request, AcquisitionOptions options)
    {
        if (request.ContentStream == null)
        {
            return (false, "ZIP archive stream is required.");
        }

        if (request.ContentLength.HasValue)
        {
            if (request.ContentLength.Value <= 0)
            {
                return (false, "ZIP file is empty.");
            }

            if (request.ContentLength.Value > options.MaxUncompressedBytes)
            {
                return (false, $"ZIP file exceeds the maximum allowed upload size of {options.MaxUncompressedBytes / (1024 * 1024)} MB.");
            }
        }

        // Verify ZIP magic bytes (PK\x03\x04 or empty archive PK\x05\x06) if stream is seekable
        if (request.ContentStream.CanSeek && request.ContentStream.Length >= 4)
        {
            var initialPosition = request.ContentStream.Position;
            try
            {
                Span<byte> header = stackalloc byte[4];
                var bytesRead = request.ContentStream.Read(header);
                request.ContentStream.Position = initialPosition;

                if (bytesRead < 4)
                {
                    return (false, "File is too small to be a valid ZIP archive.");
                }

                // PK\x03\x04 (standard zip entry) or PK\x05\x06 (empty zip) or PK\x07\x08 (spanned archive)
                var isZipMagic = header[0] == 0x50 && header[1] == 0x4B &&
                    ((header[2] == 0x03 && header[3] == 0x04) ||
                     (header[2] == 0x05 && header[3] == 0x06) ||
                     (header[2] == 0x07 && header[3] == 0x08));

                if (!isZipMagic)
                {
                    return (false, "The uploaded file is not a valid ZIP archive (invalid header signature).");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to inspect ZIP file headers: {ex.Message}");
            }
        }

        return (true, null);
    }
}
