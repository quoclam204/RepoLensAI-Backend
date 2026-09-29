using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Chat;
using RepoLens.Application.Models.RAG;

namespace RepoLens.Api.Controllers;

/// <summary>
/// Chat API for evidence-grounded AI questions about analyzed repositories (T091 / FR-009 / FR-023).
/// Endpoint: POST /api/analyses/{id}/chat (contracts/api.md Sections 25-29).
/// </summary>
[ApiController]
[Route("api/analyses/{id:guid}/chat")]
public class ChatController : ControllerBase
{
    private readonly IRagService _ragService;

    public ChatController(IRagService ragService)
    {
        _ragService = ragService;
    }

    /// <summary>
    /// Asks an AI question about the analyzed repository, strictly grounded in retrieved code evidence
    /// (T091 / FR-009 / FR-023 / NFR-AI-001).
    /// The system never fabricates answers — if evidence is insufficient, the canonical
    /// T090 insufficient-evidence response is returned with confidence "unknown".
    /// </summary>
    /// <param name="id">The unique identifier of the analysis run, enforcing strict repository tenant isolation.</param>
    /// <param name="request">The chat request containing the user's question (contracts/api.md Section 25).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A chat response containing the AI answer, confidence level, and evidence traceability
    /// (contracts/api.md Sections 26-29).
    /// </returns>
    /// <response code="200">The chat response with answer, confidence, and evidence.</response>
    /// <response code="400">The question is null, empty, or whitespace.</response>
    /// <response code="404">The analysis does not exist or has not completed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChatAsync(
        Guid id,
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new ErrorResponse(new ErrorDetail(
                Code: "INVALID_REQUEST",
                Message: "Question is required and must not be empty or whitespace."
            )));
        }

        RagRequest ragRequest = new(id, request.Question);

        // The RAG pipeline (T087-T090) enforces analysis scope, evidence validation,
        // confidence evaluation, and the canonical insufficient-evidence response.
        // Input validation errors surface as 400; analysis persistence scope checks
        // throw AnalysisNotFoundException/AnalysisNotReadyException/AnalysisFailedException
        // which the ApiExceptionMiddleware maps to 404/409; AI provider, embedding, and
        // retrieval failures propagate as 500 via the middleware (never leaking internals).
        RagResult result = await _ragService.AnswerQuestionAsync(ragRequest, ct);

        // Map RagResult -> ChatResponse preserving T088-T090 traceability.
        var confidence = MapConfidence(result.Confidence);

        var evidence = result.Evidence
            .Select(e => new ChatEvidenceItem
            {
                File = e.File,
                Symbol = e.Symbol,
                StartLine = e.StartLine ?? 0,
                EndLine = e.EndLine ?? 0,
                Reason = e.Reason
            })
            .ToList();

        var response = new ChatResponse
        {
            Answer = result.Answer,
            Confidence = confidence,
            Evidence = evidence
        };

        return Ok(response);
    }

    private static string MapConfidence(AiConfidenceLevel confidence) => confidence switch
    {
        AiConfidenceLevel.High => "high",
        AiConfidenceLevel.Medium => "medium",
        AiConfidenceLevel.Low => "low",
        AiConfidenceLevel.Unknown => "unknown",
        _ => "unknown"
    };
}
