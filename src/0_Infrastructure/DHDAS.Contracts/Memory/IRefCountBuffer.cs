using System;

namespace DHDAS.Contracts.Memory;

public interface IRefCountBuffer<out T> : IDisposable
{
    T Data { get; }
    void Retain();  // 增加计数
}