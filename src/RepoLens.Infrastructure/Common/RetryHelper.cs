using Microsoft.Extensions.Logging;

namespace RepoLens.Infrastructure.Common;

/// <summary>
/// Bounded exponential backoff retry helper for safe transient operations (T114).
/// Strictly restricted to transient external network/API operations (HTTP acquisition, Embedding, AI providers).
/// Non-transient operations (parser failures, security violations, non-idempotent DB operations) are never retried.
/// </summary>
public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? initialDelay = null,
        Func<Exception, bool>? isTransient = null,
        ILogger? logger = null,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Must be at least 1");

        var delay = initialDelay ?? TimeSpan.FromMilliseconds(200);
        var opName = operationName ?? "Operation";

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var transient = isTransient?.Invoke(ex) ?? IsDefaultTransient(ex);
                if (!transient)
                {
                    logger?.LogWarning(ex, "{Operation} failed on attempt {Attempt} with non-transient exception; will not retry.", opName, attempt);
                    throw;
                }

                logger?.LogWarning(ex, "{Operation} failed on attempt {Attempt}/{MaxAttempts} (transient). Backing off for {DelayMs}ms.",
                    opName, attempt, maxAttempts, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 5000));
            }
        }

        // Final attempt execution (exceptions will propagate)
        return await operation(cancellationToken);
    }

    private static bool IsDefaultTransient(Exception ex)
    {
        return ex is HttpRequestException
            || ex is TimeoutException
            || ex is TaskCanceledException { CancellationToken.IsCancellationRequested: false };
    }
}
