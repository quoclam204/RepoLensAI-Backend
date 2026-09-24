using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Evidence;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/evidence")]
public class EvidenceController : ControllerBase
{
    private readonly IEvidenceService _evidenceService;

    public EvidenceController(IEvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    /// <summary>
    /// Returns paginated evidence records matching filters (T069 - contracts/api.md Section 24).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EvidenceDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvidences(
        Guid id,
        [FromQuery] EvidenceFilterParams filter,
        CancellationToken ct)
    {
        var result = await _evidenceService.GetEvidencesAsync(id, filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns source evidence detail (T069 - contracts/api.md Section 23).
    /// </summary>
    [HttpGet("{evidenceId:guid}")]
    [ProducesResponseType(typeof(EvidenceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidenceDetail(
        Guid id,
        Guid evidenceId,
        CancellationToken ct)
    {
        var result = await _evidenceService.GetEvidenceDetailAsync(id, evidenceId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "EVIDENCE_NOT_FOUND",
                Message: $"Evidence '{evidenceId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }
}
