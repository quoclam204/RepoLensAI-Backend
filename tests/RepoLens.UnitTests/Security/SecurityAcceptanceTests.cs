using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoLens.Analysis.Security;
using RepoLens.Application.Abstractions;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Acquisition;
using RepoLens.Infrastructure.Storage;

namespace RepoLens.UnitTests.Security;

/// <summary>
/// Security Acceptance Tests covering T103 & T123 (NFR-SEC-001, NFR-SEC-002).
/// Verifies Zip Slip prevention, secret redaction, resource limit enforcement,
/// and cross-analysis repository isolation.
/// </summary>
public class SecurityAcceptanceTests : IDisposable
{
    private readonly string _testDir;

    public SecurityAcceptanceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RepoLens_Sec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public async Task T103_T123_MaliciousZipSlip_EntryIsStrictlyRejected()
    {
        // Arrange: Create malicious ZIP containing a path traversal entry
        var zipPath = Path.Combine(_testDir, "zipslip.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../../evil_script.sh");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("echo malicious payload");
        }

        var workspace = new TemporaryWorkspace(Guid.NewGuid(), Path.Combine(_testDir, "ws_slip"), NullLogger<TemporaryWorkspace>.Instance);
        var options = Options.Create(new AcquisitionOptions());
        var zipSource = new ZipRepositorySource(options, NullLogger<ZipRepositorySource>.Instance);

        using var stream = File.OpenRead(zipPath);
        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: stream,
            FileName: "zipslip.zip",
            ContentLength: stream.Length);

        // Act
        var result = await zipSource.AcquireAsync(request, workspace);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("traversal", result.ErrorMessage?.ToLowerInvariant() ?? "");
        Assert.False(File.Exists(Path.Combine(_testDir, "evil_script.sh")));
    }

    [Fact]
    public void T103_T123_SecretMasking_RedactsCredentialsAndPrivateKeys()
    {
        // Arrange
        var codeWithSecrets = """
            public class Config
            {
                public string ApiKey = "dummy_super_secret_api_key_987654321";
                public string ConnStr = "Server=myServer;Database=myDataBase;Uid=myUsername;Pwd=mySecretPassword123;";
                public string BearerToken = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
                public string PrivateKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0...\n-----END RSA PRIVATE KEY-----";
            }
            """;

        // Act
        var masked = SecretMasker.MaskSecrets(codeWithSecrets);

        // Assert
        Assert.DoesNotContain("mySecretPassword123", masked);
        Assert.DoesNotContain("dummy_super_secret_api_key_987654321", masked);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", masked);
        Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", masked);
        Assert.Contains("***MASKED***", masked);
    }

    [Fact]
    public void T103_T123_ResourceLimits_RejectsOversizedArchiveUpload()
    {
        // Arrange
        var options = new AcquisitionOptions { MaxUncompressedBytes = 1024 * 1024 }; // 1MB limit
        var oversizedLength = 2 * 1024 * 1024; // 2MB

        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: new MemoryStream(),
            FileName: "large.zip",
            ContentLength: oversizedLength);

        // Act
        var (isValid, error) = RepositoryValidator.Validate(request, options);

        // Assert
        Assert.False(isValid);
        Assert.Contains("maximum allowed upload size", error ?? "");
    }

    [Fact]
    public async Task T103_T123_DecompressionBomb_AbortsWhenUncompressedBytesExceedLimitDuringDecompression()
    {
        // Arrange: Create a zip with 100KB payload but options restrict to 50KB
        var options = new AcquisitionOptions
        {
            MaxUncompressedBytes = 50 * 1024,
            MaxSingleFileBytes = 50 * 1024
        };

        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("payload.txt", CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            var payload = new byte[80 * 1024]; // 80 KB
            Array.Fill(payload, (byte)'A');
            entryStream.Write(payload);
        }
        zipStream.Position = 0;

        var source = new ZipRepositorySource(
            Options.Create(options),
            NullLogger<ZipRepositorySource>.Instance);

        var workspace = new TemporaryWorkspace(Guid.NewGuid(), Path.Combine(_testDir, "bomb_ws"), NullLogger<TemporaryWorkspace>.Instance);

        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: zipStream,
            FileName: "bomb.zip",
            ContentLength: zipStream.Length);

        // Act
        var result = await source.AcquireAsync(request, workspace);

        // Assert: Extraction should be aborted
        Assert.False(result.Success);
        Assert.Contains("exceeds maximum", result.ErrorMessage ?? "");
    }
}
