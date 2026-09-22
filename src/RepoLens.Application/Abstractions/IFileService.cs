using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Files;

namespace RepoLens.Application.Abstractions;

public interface IFileService
{
    Task<PagedResult<FileItemDto>> GetFilesAsync(Guid analysisId, FileFilterParams filter, CancellationToken ct = default);
    Task<FileDetailResponse?> GetFileDetailAsync(Guid analysisId, Guid fileId, CancellationToken ct = default);
    Task<FileContentResponse?> GetFileContentAsync(Guid analysisId, Guid fileId, CancellationToken ct = default);
}
