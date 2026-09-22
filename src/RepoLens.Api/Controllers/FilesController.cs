using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Files;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/files")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Returns repository files with filtering and pagination (T067 - contracts/api.md Section 19).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FileItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiles(
        Guid id,
        [FromQuery] FileFilterParams filter,
        CancellationToken ct)
    {
        var result = await _fileService.GetFilesAsync(id, filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns file metadata and symbols (T067 - contracts/api.md Section 20).
    /// </summary>
    [HttpGet("{fileId:guid}")]
    [ProducesResponseType(typeof(FileDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileDetail(
        Guid id,
        Guid fileId,
        CancellationToken ct)
    {
        var result = await _fileService.GetFileDetailAsync(id, fileId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "FILE_NOT_FOUND",
                Message: $"File '{fileId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns source content for a file (T067 - contracts/api.md Section 21).
    /// </summary>
    [HttpGet("{fileId:guid}/content")]
    [ProducesResponseType(typeof(FileContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileContent(
        Guid id,
        Guid fileId,
        CancellationToken ct)
    {
        var result = await _fileService.GetFileContentAsync(id, fileId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "FILE_NOT_FOUND",
                Message: $"File '{fileId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }
}
