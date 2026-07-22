using Financial.Robot.Domain.Events;
using System.Threading.Channels;

namespace Financial.Robot.Application.Interfaces;

public interface IMarketDataService
{
    TickSubscription AssinarTicks();
}

public sealed class TickSubscription : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    public TickSubscription(ChannelReader<TickEvent> reader, Action dispose)
    {
        Reader = reader;
        _dispose = dispose;
    }

    public ChannelReader<TickEvent> Reader { get; }

    public void Dispose()
    {
        if (_disposed) return;

        _dispose();
        _disposed = true;
    }
}
