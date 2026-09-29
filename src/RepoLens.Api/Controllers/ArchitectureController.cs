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
    private readonly IArchifyAdapter _archifyAdapter;

    public ArchitectureController(
        IArchitectureService architectureService,
        IArchifyAdapter archifyAdapter)
    {
        _architectureService = architectureService;
        _archifyAdapter = archifyAdapter;
    }

    /// <summary>
    /// Returns architecture nodes and relationships (T063 - contracts/api.md Section 10).
    /// Supports ?format=archify for Archify-compatible C4 specification.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ArchitectureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RepoLens.Application.Models.Archify.ArchifyDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetArchitecture(Guid id, [FromQuery] string? format, CancellationToken ct)
    {
        var architecture = await _architectureService.GetArchitectureAsync(id, ct);
        if (architecture == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        if (string.Equals(format, "archify", StringComparison.OrdinalIgnoreCase))
        {
            var archifyDoc = _archifyAdapter.ConvertFromArchitectureResponse(architecture);
            return Ok(archifyDoc);
        }

        return Ok(architecture);
    }

    /// <summary>
    /// Returns the Archify-compatible C4 architectural document directly (T063 / Archify schema).
    /// </summary>
    [HttpGet("archify")]
    [ProducesResponseType(typeof(RepoLens.Application.Models.Archify.ArchifyDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArchifyArchitecture(Guid id, CancellationToken ct)
    {
        var architecture = await _architectureService.GetArchitectureAsync(id, ct);
        if (architecture == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        var archifyDoc = _archifyAdapter.ConvertFromArchitectureResponse(architecture);
        return Ok(archifyDoc);
    }
}
