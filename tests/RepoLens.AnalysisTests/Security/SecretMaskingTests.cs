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
        Assert.Contains("***MASKED***", sanitized);
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
