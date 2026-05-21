using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using Microsoft.Extensions.Hosting;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using DHDAS.Contracts.Memory;
using DHDAS.Infrastructure.Core.Session;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal;

public class DataPushService : BasePipelineNode, IDataPushService
{
    public override string NodeId => nameof(DataPushService);
    private int _disposedValue = 0;

    private readonly SessionManager _sessionManager;
    private readonly Subject<RefCountBuffer<RawDataPacket>> _dataSubject = new();
    private readonly ReaderWriterLockSlim _lock = new();

    // 实现 IDataPushService 接口供 UI 订阅
    public IObservable<RefCountBuffer<RawDataPacket>> OnRawDataReceived => _dataSubject;

    public DataPushService(SessionManager sessionManager)
    {
        Console.WriteLine($"[系统] 节点 {NodeId} 已创建，等待编排连接...");
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 实现管道节点的处理逻辑：将流经本节点的数据广播出去
    /// </summary>
    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        if (_disposedValue == 1) return false;

        _lock.EnterReadLock();
        try
        {
            // 1. 分发给全局 Rx 订阅者 (UI 插件)
            // 必须先 Retain，因为 Rx 是异步分发的，必须保证回调执行前内存不回收
            refBuffer.Retain();
            _dataSubject.OnNext(refBuffer);

            // 2. 分发给 Session 专属总线 (100通道高性能处理)
            var session = _sessionManager.GetSession(refBuffer.Data.ChannelId);
            refBuffer.Retain();
            session.Push(refBuffer);

            // 3. 返回 true：允许数据继续流向配置文件中定义的下一个节点（如存储模块）
            // 这样就实现了：采集 -> 推送(看一眼) -> 存储(存盘) 的流水线
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DataPush] 分发异常: {ex.Message}");
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public override void Dispose()
    {
        // 使用原子操作，确保只有第一次进入时返回 0
        if (Interlocked.Exchange(ref _disposedValue, 1) == 1)
        {
            return;
        }

        try
        {
            // 1. 先通知订阅者：数据流已结束
            _dataSubject.OnCompleted();

            // 2. 销毁 Rx 对象
            _dataSubject.Dispose();

            // 3. 销毁读写锁
            _lock.Dispose();
        }
        catch (Exception ex)
        {
            // 忽略释放过程中的次生异常
            Console.WriteLine($"[释放] DataPushService 释放期间发生异常: {ex.Message}");
        }
        finally
        {
            // 4. 最后调用基类的 Dispose（基类负责取消 CancellationToken）
            base.Dispose();
        }
    }
}