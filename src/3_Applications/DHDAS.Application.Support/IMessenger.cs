using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DHDAS.Application.Support;

public interface IMessenger
{
    void Send<T>(T message);
    IObservable<T> Listen<T>();
}

public class AppMessenger : IMessenger, IDisposable
{
    private readonly Subject<object> _messages = new();

    public void Send<T>(T message)
    {
        Console.WriteLine($"[MessageBus] Send message: {typeof(T).Name}");
        if (message is not null)
        {
            _messages.OnNext(message);
        }
    }

    public IObservable<T> Listen<T>() => _messages.OfType<T>();

    public void Dispose() => _messages.Dispose();
}
