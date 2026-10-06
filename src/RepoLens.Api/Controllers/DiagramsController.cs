using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Diagrams;

namespace RepoLens.Api.Controllers;

/// <summary>
/// Controller for repository-type-specific diagrams (Giai đoạn 2).
/// Delivers customized diagrams (Architecture, Endpoints, ERD, RouteMap, SystemOverview, NamespaceClass, CallFlow)
/// matching each repository type according to Archify visual specifications.
/// </summary>
[ApiController]
[Route("api/analyses/{id:guid}/diagrams")]
public class DiagramsController : ControllerBase
{
    private readonly IDiagramService _diagramService;

    public DiagramsController(IDiagramService diagramService)
    {
        _diagramService = diagramService;
    }

    /// <summary>
    /// Returns the primary diagram for the repository type (default view).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDefaultDiagram(Guid id, CancellationToken ct)
    {
        var diagram = await _diagramService.GetDiagramAsync(id, diagramType: null, ct);
        return Ok(diagram);
    }

    /// <summary>
    /// Returns the specific requested diagram type (e.g. "architecture", "endpoints", "erd", "route_map", "system_overview", "namespace_class", "call_flow").
    /// If evidence is absent (e.g. ERD requested without DbContext), returns status "NotDetected" without fabricating components.
    /// </summary>
    [HttpGet("{diagramType}")]
    [ProducesResponseType(typeof(DiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDiagram(Guid id, string diagramType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(diagramType))
        {
            return BadRequest(new ErrorResponse(new ErrorDetail(
                Code: "INVALID_DIAGRAM_TYPE",
                Message: "Diagram type cannot be empty."
            )));
        }

        var diagram = await _diagramService.GetDiagramAsync(id, diagramType, ct);
        return Ok(diagram);
    }
}
