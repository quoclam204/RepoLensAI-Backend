namespace RepoLens.Application.Common;

/// <summary>
/// Standard paginated response envelope matching contracts/api.md Section 32.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
