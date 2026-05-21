using System;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;

namespace DHDAS.Contracts.Services;

public interface IDataPushService
{
    // 提供一个响应式流，供多个 UI 插件订阅
    IObservable<RefCountBuffer<RawDataPacket>> OnRawDataReceived { get; }
}