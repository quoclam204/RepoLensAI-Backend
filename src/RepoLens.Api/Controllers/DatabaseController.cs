using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Database;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/database")]
public class DatabaseController : ControllerBase
{
    private readonly IDatabaseModelService _databaseModelService;

    public DatabaseController(IDatabaseModelService databaseModelService)
    {
        _databaseModelService = databaseModelService;
    }

    /// <summary>
    /// Returns detected database entities and relationships (T066 - contracts/api.md Section 17).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DatabaseModelResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatabaseModel(Guid id, CancellationToken ct)
    {
        var result = await _databaseModelService.GetDatabaseModelAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns detailed database entity information (T066 - contracts/api.md Section 18).
    /// </summary>
    [HttpGet("entities/{entityId:guid}")]
    [ProducesResponseType(typeof(DatabaseEntityDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntityDetail(
        Guid id,
        Guid entityId,
        CancellationToken ct)
    {
        var result = await _databaseModelService.GetEntityDetailAsync(id, entityId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "ENTITY_NOT_FOUND",
                Message: $"Database entity '{entityId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }
}
