using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Analyses;
using RepoLens.Application.DTOs.Overview;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public class AnalysesController : ControllerBase
{
    private readonly IAnalysisService _analysisService;

    public AnalysesController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>
    /// Creates a new repository analysis (T060 - contracts/api.md Section 6).
    /// Supports GitUrl (JSON) or ZipUpload (multipart/form-data).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateAnalysisResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAnalysis([FromBody] CreateAnalysisGitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            return BadRequest(new ErrorResponse(new ErrorDetail(
                Code: "INVALID_REQUEST",
                Message: "sourceUrl is required for Git repository analysis."
            )));
        }

        var result = await _analysisService.CreateAnalysisFromGitAsync(request, ct);
        return Accepted($"/api/analyses/{result.AnalysisId}", result);
    }

    /// <summary>
    /// Creates a new repository analysis from uploaded ZIP archive (T060 - contracts/api.md Section 6.2).
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CreateAnalysisResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadZip([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse(new ErrorDetail(
                Code: "INVALID_ZIP",
                Message: "A valid non-empty zip file must be provided."
            )));
        }

        using var stream = file.OpenReadStream();
        var result = await _analysisService.CreateAnalysisFromZipAsync(file.FileName, stream, ct);
        return Accepted($"/api/analyses/{result.AnalysisId}", result);
    }

    /// <summary>
    /// Returns the current state and progress of an analysis (T061 - contracts/api.md Section 7).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnalysisStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var status = await _analysisService.GetAnalysisStatusAsync(id, ct);
        if (status == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        return Ok(status);
    }

    /// <summary>
    /// Returns high-level statistics and repository overview (T062 - contracts/api.md Section 9).
    /// </summary>
    [HttpGet("{id:guid}/overview")]
    [ProducesResponseType(typeof(AnalysisOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetOverview(Guid id, CancellationToken ct)
    {
        var overview = await _analysisService.GetAnalysisOverviewAsync(id, ct);
        if (overview == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        return Ok(overview);
    }
}
