using System.Text.RegularExpressions;

namespace RepoLens.Infrastructure.Scanning;

/// <summary>
/// Secret detection boundary (T034, NFR-SEC-001, NFR-003).
/// Detects files containing API keys, private certificates, credentials, and sensitive environment variables.
/// Critical rule: Detected secret files MUST NOT be sent to AI providers or indexed into RAG embeddings.
/// </summary>
public sealed class SecretDetector
{
    private static readonly HashSet<string> SecretExactFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        ".env.local",
        ".env.development",
        ".env.production",
        ".env.staging",
        ".env.test",
        "secrets.json",
        "appsettings.secrets.json",
        "credentials.json",
        "id_rsa",
        "id_dsa",
        "id_ed25519",
        "id_ecdsa"
    };

    private static readonly HashSet<string> SecretExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem",
        ".key",
        ".pfx",
        ".p12",
        ".pkcs12",
        ".asc",
        ".cer",
        ".crt"
    };

    // Fast heuristic regexes for high-confidence secrets
    private static readonly Regex PrivateKeyHeaderRegex = new(
        @"-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AwsKeyRegex = new(
        @"\b(?:AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Checks whether a file path/name corresponds to a known secret configuration file.
    /// </summary>
    public bool IsSecretFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var baseName = Path.GetFileName(fileName);
        if (SecretExactFileNames.Contains(baseName))
        {
            return true;
        }

        // Check if starts with .env.
        if (baseName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(baseName);
        if (!string.IsNullOrEmpty(extension) && SecretExtensions.Contains(extension))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Inspects the first few kilobytes of a text file to detect private keys or credentials.
    /// </summary>
    public async Task<bool> ContainsSecretContentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            // Only inspect up to first 8KB to avoid overhead
            var buffer = new char[8192];
            using var reader = new StreamReader(filePath);
            var read = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                return false;
            }

            var sample = new string(buffer, 0, read);

            if (PrivateKeyHeaderRegex.IsMatch(sample))
            {
                return true;
            }

            if (AwsKeyRegex.IsMatch(sample))
            {
                return true;
            }

            return false;
        }
        catch
        {
            // If file cannot be read, treat as non-secret (or let scanner handle as unreadable)
            return false;
        }
    }
}
