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

        if (string.Equals(format, "v3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, "archify-v3", StringComparison.OrdinalIgnoreCase))
        {
            var archifyV3Doc = _archifyAdapter.ConvertToArchifyV3(architecture);
            return Ok(archifyV3Doc);
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

    /// <summary>
    /// Returns the official Archify V3 diagram specification document with swimlane regions and component semantics.
    /// </summary>
    [HttpGet("v3")]
    [ProducesResponseType(typeof(RepoLens.Application.Models.Archify.ArchifyV3Document), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArchifyV3(Guid id, CancellationToken ct)
    {
        var architecture = await _architectureService.GetArchitectureAsync(id, ct);
        if (architecture == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        var archifyV3 = _archifyAdapter.ConvertToArchifyV3(architecture);
        return Ok(archifyV3);
    }

    /// <summary>
    /// Generates a standalone, portable interactive HTML diagram matching the Archify viewer.
    /// </summary>
    [HttpGet("export/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportArchifyHtml(Guid id, [FromQuery] string theme = "dark", CancellationToken ct = default)
    {
        var architecture = await _architectureService.GetArchitectureAsync(id, ct);
        if (architecture == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        var archifyV3 = _archifyAdapter.ConvertToArchifyV3(architecture);
        var html = _archifyAdapter.GenerateStandaloneHtml(archifyV3, theme);
        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Traces the shortest path between two components in the architecture knowledge graph.
    /// </summary>
    [HttpGet("trace")]
    [ProducesResponseType(typeof(ArchitectureTraceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TraceRoute(Guid id, [FromQuery] string from, [FromQuery] string to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return BadRequest(new ErrorResponse(new ErrorDetail(
                Code: "INVALID_PARAMETERS",
                Message: "Both 'from' and 'to' node identifiers are required."
            )));
        }

        var traceResult = await _architectureService.TracePathAsync(id, from, to, ct);
        if (traceResult == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ANALYSIS_NOT_FOUND",
                Message: $"The requested analysis '{id}' does not exist."
            )));
        }

        return Ok(traceResult);
    }
}

