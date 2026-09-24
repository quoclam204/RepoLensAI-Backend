using System.Diagnostics;
using System.Text.Json;
using RepoLens.Application.Common;

namespace RepoLens.Api.Middleware;

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        int statusCode;
        ErrorResponse response;

        switch (exception)
        {
            case AnalysisNotFoundException notFound:
                statusCode = StatusCodes.Status404NotFound;
                response = new ErrorResponse(new ErrorDetail(
                    Code: "ANALYSIS_NOT_FOUND",
                    Message: notFound.Message,
                    Details: null,
                    TraceId: traceId));
                break;

            case AnalysisNotReadyException notReady:
                statusCode = StatusCodes.Status409Conflict;
                response = new ErrorResponse(new ErrorDetail(
                    Code: "ANALYSIS_NOT_READY",
                    Message: notReady.Message,
                    Details: new { status = notReady.Status },
                    TraceId: traceId));
                break;

            case AnalysisFailedException failed:
                statusCode = StatusCodes.Status409Conflict;
                response = new ErrorResponse(new ErrorDetail(
                    Code: "ANALYSIS_FAILED",
                    Message: failed.Message,
                    Details: new { stage = failed.Stage },
                    TraceId: traceId));
                break;

            case ArgumentException argEx:
                statusCode = StatusCodes.Status400BadRequest;
                response = new ErrorResponse(new ErrorDetail(
                    Code: "INVALID_REQUEST",
                    Message: argEx.Message,
                    Details: null,
                    TraceId: traceId));
                break;

            default:
                _logger.LogError(exception, "Unhandled exception occurred while processing request");
                statusCode = StatusCodes.Status500InternalServerError;
                response = new ErrorResponse(new ErrorDetail(
                    Code: "INTERNAL_SERVER_ERROR",
                    Message: "An unexpected error occurred. Please try again later.",
                    Details: null,
                    TraceId: traceId));
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
