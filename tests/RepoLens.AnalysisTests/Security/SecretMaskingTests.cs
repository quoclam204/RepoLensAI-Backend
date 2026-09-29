using RepoLens.Analysis.Evidence;
using RepoLens.Analysis.Security;
using RepoLens.Domain.Enums;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.Security;

public class SecretMaskingTests
{
    [Theory]
    [InlineData("string connectionString = \"Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=SuperSecretPassword123!\";")]
    [InlineData("const string apiKey = \"sk-proj-abc123XYZ9876543210\";")]
    [InlineData("var token = \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\";")]
    [InlineData("private string password = \"P@ssw0rd2026!\";")]
    [InlineData("string client_secret = \"super_secret_client_credential\";")]
    [InlineData("string awsKey = \"AKIAIOSFODNN7EXAMPLE\";")]
    [InlineData("string ghToken = \"ghp_1234567890abcdefghijklmnopqrstuvwxyzAB\";")]
    [InlineData("string access_token = \"my_secret_access_token_12345\";")]
    [InlineData("string refresh_token = \"my_secret_refresh_token_67890\";")]
    [InlineData("string conn = \"Server=myServer;SecretKey=super_secret_key;AccessKey=my_access_key;\";")]
    public void SecretMasker_MasksSensitiveCredentials(string inputSnippet)
    {
        // Act
        var sanitized = SecretMasker.MaskSecrets(inputSnippet);

        // Assert
        Assert.DoesNotContain("SuperSecretPassword123!", sanitized);
        Assert.DoesNotContain("sk-proj-abc123XYZ9876543210", sanitized);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", sanitized);
        Assert.DoesNotContain("P@ssw0rd2026!", sanitized);
        Assert.DoesNotContain("super_secret_client_credential", sanitized);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", sanitized);
        Assert.DoesNotContain("ghp_1234567890abcdefghijklmnopqrstuvwxyzAB", sanitized);
        Assert.DoesNotContain("my_secret_access_token_12345", sanitized);
        Assert.DoesNotContain("my_secret_refresh_token_67890", sanitized);
        Assert.DoesNotContain("super_secret_key", sanitized);
        Assert.DoesNotContain("my_access_key", sanitized);
        Assert.Contains("***MASKED***", sanitized);
    }

    [Theory]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0Y1+xyz\n-----END RSA PRIVATE KEY-----")]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmU\n-----END OPENSSH PRIVATE KEY-----")]
    [InlineData("-----BEGIN EC PRIVATE KEY-----\nMHQCAQEEIB12345\n-----END EC PRIVATE KEY-----")]
    public void SecretMasker_MasksPrivateKeyBlocks(string keySnippet)
    {
        // Act
        var sanitized = SecretMasker.MaskSecrets(keySnippet);

        // Assert
        Assert.DoesNotContain("MIIEowIBAAKCAQEA0Y1+xyz", sanitized);
        Assert.DoesNotContain("b3BlbnNzaC1rZXktdjEAAAAABG5vbmU", sanitized);
        Assert.DoesNotContain("MHQCAQEEIB12345", sanitized);
        Assert.Contains("***MASKED PRIVATE KEY***", sanitized);
    }

    [Fact]
    public void EvidenceFactory_SanitizeSnippet_AppliesSecretMaskingAutomatically()
    {
        // Arrange
        var rawSnippet = "public void Connect() { var password = \"VerySecret123\"; }";

        // Act
        var evidence = EvidenceFactory.Create(
            analysisJobId: Guid.NewGuid(),
            filePath: "Services/AuthService.cs",
            startLine: 10,
            endLine: 12,
            snippet: rawSnippet,
            evidenceType: EvidenceType.Declaration,
            confidence: ConfidenceScore.High);

        // Assert
        Assert.DoesNotContain("VerySecret123", evidence.Description);
        Assert.Contains("***MASKED***", evidence.Description);
    }
}
