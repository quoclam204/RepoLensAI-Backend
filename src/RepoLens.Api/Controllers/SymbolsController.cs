using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Symbols;

namespace RepoLens.Api.Controllers;

[ApiController]
[Route("api/analyses/{id:guid}/symbols")]
public class SymbolsController : ControllerBase
{
    private readonly ISymbolService _symbolService;

    public SymbolsController(ISymbolService symbolService)
    {
        _symbolService = symbolService;
    }

    /// <summary>
    /// Returns detailed symbol information (T068 - contracts/api.md Section 22).
    /// </summary>
    [HttpGet("{symbolId:guid}")]
    [ProducesResponseType(typeof(SymbolDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSymbolDetail(
        Guid id,
        Guid symbolId,
        CancellationToken ct)
    {
        var result = await _symbolService.GetSymbolDetailAsync(id, symbolId, ct);
        if (result == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                Code: "SYMBOL_NOT_FOUND",
                Message: $"Symbol '{symbolId}' does not exist for analysis '{id}'."
            )));
        }

        return Ok(result);
    }
}
