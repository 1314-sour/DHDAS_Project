using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DHDAS.Application.Support;

/// <summary>
/// 消息中转站接口
/// </summary>
public interface IMessenger
{
    void Send<T>(T message);

    IDisposable Subscribe<T>(Action<T> handler);
}

/// <summary>
/// 消息中转站的简单实现（Demo期使用）
/// </summary>
public class AppMessenger : IMessenger
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Send<T>(T message)
    {
        List<Delegate> handlers;
        lock (_lock)
        {
            handlers = _handlers.TryGetValue(typeof(T), out var list)
                ? list.ToList()
                : new List<Delegate>();
        }

        foreach (var handler in handlers.OfType<Action<T>>())
        {
            handler(message);
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }

            list.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(T), out var list))
                {
                    list.Remove(handler);
                }
            }
        });
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
