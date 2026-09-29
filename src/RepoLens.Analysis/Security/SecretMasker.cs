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
    [GeneratedRegex(@"(?i)(Password|Pwd|User ID|Uid|AccountKey|SharedAccessKey|SecretKey|AccessKey)(=)([^;]+)")]
    private static partial Regex ConnectionStringPattern();

    // Quoted credential assignments: key = "value" or key: 'value'
    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|access_token|refresh_token|auth_token|apikey|api_key|secretkey|secret_key|accesskey|access_key|client_secret|private_key)(\s*[:=]\s*[""'])([^""'\r\n]+)([""'])")]
    private static partial Regex QuotedCredentialPattern();

    // Unquoted credential assignments: key=value
    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|access_token|refresh_token|auth_token|apikey|api_key|secretkey|secret_key|accesskey|access_key|client_secret|private_key)(\s*[:=]\s*)([^\s;,]+)")]
    private static partial Regex UnquotedCredentialPattern();

    // Private key block: -----BEGIN ... PRIVATE KEY----- ... -----END ... PRIVATE KEY-----
    [GeneratedRegex(@"-----BEGIN (?:[A-Z0-9_-]+ )?PRIVATE KEY-----[\s\S]*?-----END (?:[A-Z0-9_-]+ )?PRIVATE KEY-----")]
    private static partial Regex PrivateKeyBlockPattern();

    // Vendor API key: AWS Access Key ID
    [GeneratedRegex(@"(?<![A-Z0-9])AKIA[0-9A-Z]{16}(?![A-Z0-9])")]
    private static partial Regex AwsAccessKeyPattern();

    // Vendor API key: GitHub Token (ghp, gho, ghu, ghs, ghr)
    [GeneratedRegex(@"(?<![A-Za-z0-9])gh[pousr]_[A-Za-z0-9_]{36,}(?![A-Za-z0-9_])")]
    private static partial Regex GitHubTokenPattern();

    // Vendor API key: OpenAI / Anthropic key (sk-...)
    [GeneratedRegex(@"(?<![A-Za-z0-9])sk-[a-zA-Z0-9\-_]{20,}(?![A-Za-z0-9\-_])")]
    private static partial Regex OpenAiKeyPattern();

    /// <summary>
    /// Scans snippet text and masks detected sensitive values with ***MASKED***.
    /// </summary>
    public static string MaskSecrets(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var result = PrivateKeyBlockPattern().Replace(text, "***MASKED PRIVATE KEY***");
        result = AwsAccessKeyPattern().Replace(result, "***MASKED***");
        result = GitHubTokenPattern().Replace(result, "***MASKED***");
        result = OpenAiKeyPattern().Replace(result, "***MASKED***");
        result = BearerTokenPattern().Replace(result, m => $"{m.Groups[1].Value}***MASKED***");
        result = ConnectionStringPattern().Replace(result, m => $"{m.Groups[1].Value}={("***MASKED***")}");
        result = QuotedCredentialPattern().Replace(result, m => $"{m.Groups[1].Value}{m.Groups[2].Value}***MASKED***{m.Groups[4].Value}");
        result = UnquotedCredentialPattern().Replace(result, m => $"{m.Groups[1].Value}{m.Groups[2].Value}***MASKED***");

        return result;
    }
}
