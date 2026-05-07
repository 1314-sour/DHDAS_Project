namespace DHDAS.Contracts.Memory;

public sealed class RefCountBuffer<T> : IRefCountBuffer<T>
{
    private int _refCount = 0;
    private readonly T _data;
    private readonly Action<T> _onRelease;

    public RefCountBuffer(T data, Action<T> onRelease)
    {
        _data = data;
        _onRelease = onRelease;
    }

    public T Data => _data;

    public void Retain() => Interlocked.Increment(ref _refCount);

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) <= 0)
        {
            _onRelease(_data); // 计数归零，执行回收（还回池）
        }
    }
}