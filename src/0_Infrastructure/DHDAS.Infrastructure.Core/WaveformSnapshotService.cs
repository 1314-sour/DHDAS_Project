using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;

namespace DHDAS.Infrastructure.Core;

public class WaveformSnapshotService : IWaveformSnapshotService
{
    private readonly object _lock = new();
    private WaveformSnapshot? _latest;
    private event Action<WaveformSnapshot>? SnapshotReceived;

    public WaveformSnapshot? Latest
    {
        get
        {
            lock (_lock)
            {
                return _latest;
            }
        }
    }

    public IDisposable Subscribe(Action<WaveformSnapshot> handler)
    {
        WaveformSnapshot? snapshot;
        lock (_lock)
        {
            SnapshotReceived += handler;
            snapshot = _latest;
        }

        if (snapshot != null)
        {
            handler(snapshot);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                SnapshotReceived -= handler;
            }
        });
    }

    public void Publish(WaveformSnapshot snapshot)
    {
        Action<WaveformSnapshot>? handlers;
        lock (_lock)
        {
            _latest = snapshot;
            handlers = SnapshotReceived;
        }

        handlers?.Invoke(snapshot);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        private int _disposed;

        public Subscription(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _dispose();
            }
        }
    }
}
