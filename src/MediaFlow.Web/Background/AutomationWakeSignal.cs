using System.Threading.Channels;

namespace MediaFlow.Web.Background;

public sealed class AutomationWakeSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public void Wake() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan maximumDelay, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(maximumDelay);

        try
        {
            await _channel.Reader.ReadAsync(timeout.Token);
            while (_channel.Reader.TryRead(out _)) { }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Periodic reconciliation timeout reached.
        }
    }
}
