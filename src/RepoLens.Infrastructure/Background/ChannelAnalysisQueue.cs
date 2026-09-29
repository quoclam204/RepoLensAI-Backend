using System.Threading.Channels;
using RepoLens.Application.Abstractions;

namespace RepoLens.Infrastructure.Background;

/// <summary>
/// Thread-safe bounded channel implementation of <see cref="IAnalysisQueue"/> (T054).
/// Limits concurrent queued jobs to prevent unbounded memory growth during high load.
/// </summary>
public sealed class ChannelAnalysisQueue : IAnalysisQueue
{
    private readonly Channel<AnalysisWorkItem> _channel;

    public ChannelAnalysisQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity > 0 ? capacity : 100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<AnalysisWorkItem>(options);
    }

    /// <inheritdoc />
    public ValueTask EnqueueAsync(AnalysisWorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return _channel.Writer.WriteAsync(workItem, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<AnalysisWorkItem> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
