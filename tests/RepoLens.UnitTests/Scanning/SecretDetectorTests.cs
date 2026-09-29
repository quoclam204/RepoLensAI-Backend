using RepoLens.Infrastructure.Scanning;
using Xunit;

namespace RepoLens.UnitTests.Scanning;

public class SecretDetectorTests : IDisposable
{
    private readonly SecretDetector _detector = new();
    private readonly string _tempDir;

    public SecretDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "repolens-secret-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData(".env.production")]
    [InlineData("secrets.json")]
    [InlineData("appsettings.secrets.json")]
    [InlineData("id_rsa")]
    [InlineData("id_ed25519")]
    [InlineData("server.key")]
    [InlineData("certificate.pem")]
    [InlineData("client.pfx")]
    public void IsSecretFile_WithKnownSecretFilenamesAndExtensions_ReturnsTrue(string fileName)
    {
        Assert.True(_detector.IsSecretFile(fileName));
    }

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("appsettings.json")]
    [InlineData("package.json")]
    [InlineData("README.md")]
    [InlineData("Repository.cs")]
    public void IsSecretFile_WithNormalFiles_ReturnsFalse(string fileName)
    {
        Assert.False(_detector.IsSecretFile(fileName));
    }

    [Fact]
    public async Task ContainsSecretContentAsync_WithPrivateKeyHeader_ReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "key.txt");
        await File.WriteAllTextAsync(filePath, "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0...\n-----END RSA PRIVATE KEY-----");

        var result = await _detector.ContainsSecretContentAsync(filePath);

        Assert.True(result);
    }

    [Fact]
    public async Task ContainsSecretContentAsync_WithAwsKeyPattern_ReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "config.txt");
        await File.WriteAllTextAsync(filePath, "AWS_KEY=AKIAIOSFODNN7EXAMPLE");

        var result = await _detector.ContainsSecretContentAsync(filePath);

        Assert.True(result);
    }

    [Fact]
    public async Task ContainsSecretContentAsync_WithNormalCode_ReturnsFalse()
    {
        var filePath = Path.Combine(_tempDir, "App.cs");
        await File.WriteAllTextAsync(filePath, "namespace MyApp; public class App { }");

        var result = await _detector.ContainsSecretContentAsync(filePath);

        Assert.False(result);
    }
}
