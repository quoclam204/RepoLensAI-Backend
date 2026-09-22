using System.Text.RegularExpressions;

namespace RepoLens.Analysis.Security;

/// <summary>
/// Redacts sensitive credentials, tokens, API keys, and connection strings from source code snippets
/// before they are stored in Evidence or knowledge graphs.
/// </summary>
public static partial class SecretMasker
{
    // Authorization Bearer tokens
    [GeneratedRegex(@"(?i)(Bearer\s+)([A-Za-z0-9\-\._~\+\/=]+)")]
    private static partial Regex BearerTokenPattern();

    // Connection string sensitive tokens
    [GeneratedRegex(@"(?i)(Password|Pwd|User ID|Uid|AccountKey|SharedAccessKey)(=)([^;]+)")]
    private static partial Regex ConnectionStringPattern();

    // Quoted credential assignments: key = "value" or key: 'value'
    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|apikey|api_key|client_secret|private_key)(\s*[:=]\s*[""'])([^""'\r\n]+)([""'])")]
    private static partial Regex QuotedCredentialPattern();

    // Unquoted credential assignments: key=value
    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|apikey|api_key|client_secret|private_key)(\s*[:=]\s*)([^\s;,]+)")]
    private static partial Regex UnquotedCredentialPattern();

    /// <summary>
    /// Scans snippet text and masks detected sensitive values with ***MASKED***.
    /// </summary>
    public static string MaskSecrets(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var result = BearerTokenPattern().Replace(text, m => $"{m.Groups[1].Value}***MASKED***");
        result = ConnectionStringPattern().Replace(result, m => $"{m.Groups[1].Value}={("***MASKED***")}");
        result = QuotedCredentialPattern().Replace(result, m => $"{m.Groups[1].Value}{m.Groups[2].Value}***MASKED***{m.Groups[4].Value}");
        result = UnquotedCredentialPattern().Replace(result, m => $"{m.Groups[1].Value}{m.Groups[2].Value}***MASKED***");

        return result;
    }
}
