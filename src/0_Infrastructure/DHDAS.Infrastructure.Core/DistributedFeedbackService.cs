using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;

namespace DHDAS.Infrastructure.Core;

public class DistributedFeedbackService : IDistributedFeedbackService
{
    private readonly object _lock = new();
    private readonly List<DistributedFeedbackMessage> _history = new();
    private event Action<DistributedFeedbackMessage>? FeedbackReceived;

    public IDisposable Subscribe(Action<DistributedFeedbackMessage> handler)
    {
        List<DistributedFeedbackMessage> snapshot;
        lock (_lock)
        {
            FeedbackReceived += handler;
            snapshot = _history.ToList();
        }

        foreach (var message in snapshot)
        {
            handler(message);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                FeedbackReceived -= handler;
            }
        });
    }

    public void Publish(string title, string message, string level = "Info")
    {
        var feedback = new DistributedFeedbackMessage
        {
            Title = title,
            Message = message,
            Level = level,
            Timestamp = DateTime.Now
        };

        Action<DistributedFeedbackMessage>? handlers;
        lock (_lock)
        {
            _history.Add(feedback);
            if (_history.Count > 20)
            {
                _history.RemoveAt(0);
            }

            handlers = FeedbackReceived;
        }

        handlers?.Invoke(feedback);
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
