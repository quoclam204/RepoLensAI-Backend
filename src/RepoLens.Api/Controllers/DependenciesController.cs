using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Dependencies;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/dependencies")]
public class DependenciesController : ControllerBase
{
    private readonly IDependencyService _dependencyService;

    public DependenciesController(IDependencyService dependencyService)
    {
        _dependencyService = dependencyService;
    }

    /// <summary>
    /// Returns the dependency graph (T064 - contracts/api.md Section 13).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DependencyItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDependencies(
        Guid id,
        [FromQuery] DependencyFilterParams filter,
        CancellationToken ct)
    {
        var result = await _dependencyService.GetDependenciesAsync(id, filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns detailed information about a dependency (T064 - contracts/api.md Section 14).
    /// </summary>
    [HttpGet("{dependencyId:guid}")]
    [ProducesResponseType(typeof(DependencyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDependencyDetail(
        Guid id,
        Guid dependencyId,
        CancellationToken ct)
    {
        var result = await _dependencyService.GetDependencyDetailAsync(id, dependencyId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "DEPENDENCY_NOT_FOUND",
                Message: $"Dependency '{dependencyId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }
}
