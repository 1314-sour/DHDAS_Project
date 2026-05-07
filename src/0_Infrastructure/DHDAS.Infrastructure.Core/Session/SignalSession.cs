using System;
using System.Buffers;
using System.Reactive.Subjects;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;

namespace DHDAS.Infrastructure.Core.Session;

/// <summary>
/// 信号会话：代表一个独立的物理/逻辑通道。
/// 内部包含独立的内存池(隔离)和专属的数据广播总线(性能)。
/// </summary>
public class SignalSession : IDisposable
{
    public int ChannelId { get; }

    // 1. 专属内存池：隔离不同通道的内存申请，消除锁竞争
    private readonly ArrayPool<double> _privatePool;

    // 2. 专属数据总线 (Rx Subject)：该通道的订阅者只监听这里
    private readonly Subject<RefCountBuffer<RawDataPacket>> _sessionBus = new();

    // 暴露给应用层插件订阅的接口
    public IObservable<RefCountBuffer<RawDataPacket>> DataStream => _sessionBus;

    public SignalSession(int channelId)
    {
        ChannelId = channelId;
        // 创建私有池：限制最大数组长度和桶的数量，防止内存无限制膨胀
        _privatePool = ArrayPool<double>.Create(maxArrayLength: 1024 * 1024, maxArraysPerBucket: 50);
    }

    /// <summary>
    /// 借用内存
    /// </summary>
    public double[] Rent(int minimumLength) => _privatePool.Rent(minimumLength);

    /// <summary>
    /// 归还内存
    /// </summary>
    public void Return(double[] array) => _privatePool.Return(array);

    /// <summary>
    /// 【核心功能】向本通道的订阅者推送数据。
    /// 由 DataPushService 或采集服务调用。
    /// </summary>
    public void Push(RefCountBuffer<RawDataPacket> data)
    {
        // 触发 Rx 广播，所有订阅此 Session.DataStream 的 UI 插件会收到通知
        _sessionBus.OnNext(data);
    }

    public void Dispose()
    {
        _sessionBus.OnCompleted();
        _sessionBus.Dispose();
        // ArrayPool 不需要显式 Dispose
    }
}