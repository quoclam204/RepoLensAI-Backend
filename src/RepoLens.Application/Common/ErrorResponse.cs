namespace RepoLens.Application.Common;

/// <summary>
/// Standard API error envelope matching contracts/api.md Section 4.
/// </summary>
public record ErrorResponse(ErrorDetail Error);

public record ErrorDetail(
    string Code,
    string Message,
    object? Details = null,
    string? TraceId = null);
