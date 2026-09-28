using RepoLens.Application.Abstractions;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Acquisition;
using Xunit;

namespace RepoLens.UnitTests.Acquisition;

public class RepositoryValidatorTests
{
    private readonly AcquisitionOptions _options = new()
    {
        MaxUncompressedBytes = 10 * 1024 * 1024,
        MaxFileCount = 100,
        MaxSingleFileBytes = 5 * 1024 * 1024
    };

    [Theory]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("http://gitlab.com/group/subgroup/project")]
    public void ValidateGitUrl_WithValidHttpAndHttpsUrls_ReturnsSuccess(string validUrl)
    {
        var (isValid, error) = RepositoryValidator.ValidateGitUrl(validUrl);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateGitUrl_WithNullOrEmpty_ReturnsFailure(string? invalidUrl)
    {
        var (isValid, error) = RepositoryValidator.ValidateGitUrl(invalidUrl);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("ssh://git@github.com/owner/repo.git")]
    [InlineData("git@github.com:owner/repo.git")]
    [InlineData("file:///C:/projects/repo")]
    [InlineData("ftp://example.com/repo")]
    public void ValidateGitUrl_WithUnsupportedProtocol_ReturnsFailure(string unsupportedUrl)
    {
        var (isValid, error) = RepositoryValidator.ValidateGitUrl(unsupportedUrl);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("--upload-pack=evil https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo; rm -rf /")]
    [InlineData("https://github.com/owner/repo\n--exec=calc")]
    [InlineData("https://github.com/owner/repo&whoami")]
    public void ValidateGitUrl_WithDangerousInjectionCharacters_ReturnsFailure(string dangerousUrl)
    {
        var (isValid, error) = RepositoryValidator.ValidateGitUrl(dangerousUrl);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateZipUpload_WithNullStream_ReturnsFailure()
    {
        var request = new RepositorySourceRequest(RepositorySourceType.ZipUpload, ContentStream: null);

        var (isValid, error) = RepositoryValidator.ValidateZipUpload(request, _options);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateZipUpload_WithNonZipHeader_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: stream,
            ContentLength: stream.Length);

        var (isValid, error) = RepositoryValidator.ValidateZipUpload(request, _options);

        Assert.False(isValid);
        Assert.Contains("invalid header signature", error);
    }

    [Fact]
    public void ValidateZipUpload_ExceedingSizeLimit_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        var request = new RepositorySourceRequest(
            RepositorySourceType.ZipUpload,
            ContentStream: stream,
            ContentLength: _options.MaxUncompressedBytes + 100);

        var (isValid, error) = RepositoryValidator.ValidateZipUpload(request, _options);

        Assert.False(isValid);
        Assert.Contains("exceeds the maximum allowed upload size", error);
    }
}
