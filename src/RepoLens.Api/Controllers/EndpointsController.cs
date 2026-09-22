using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Endpoints;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/endpoints")]
public class EndpointsController : ControllerBase
{
    private readonly IApiEndpointService _endpointService;

    public EndpointsController(IApiEndpointService endpointService)
    {
        _endpointService = endpointService;
    }

    /// <summary>
    /// Returns detected HTTP endpoints (T065 - contracts/api.md Section 15).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EndpointItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEndpoints(
        Guid id,
        [FromQuery] EndpointFilterParams filter,
        CancellationToken ct)
    {
        var result = await _endpointService.GetEndpointsAsync(id, filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns endpoint details (T065 - contracts/api.md Section 16).
    /// </summary>
    [HttpGet("{endpointId:guid}")]
    [ProducesResponseType(typeof(EndpointDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEndpointDetail(
        Guid id,
        Guid endpointId,
        CancellationToken ct)
    {
        var result = await _endpointService.GetEndpointDetailAsync(id, endpointId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ENDPOINT_NOT_FOUND",
                Message: $"Endpoint '{endpointId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }
}
