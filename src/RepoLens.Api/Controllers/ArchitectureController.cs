using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Architecture;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/architecture")]
public class ArchitectureController : ControllerBase
{
    private readonly IArchitectureService _architectureService;

    public ArchitectureController(IArchitectureService architectureService)
    {
        _architectureService = architectureService;
    }

    /// <summary>
    /// Returns architecture nodes and relationships (T063 - contracts/api.md Section 10).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ArchitectureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetArchitecture(Guid id, CancellationToken ct)
    {
        var architecture = await _architectureService.GetArchitectureAsync(id, ct);
        if (architecture == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        return Ok(architecture);
    }
}
