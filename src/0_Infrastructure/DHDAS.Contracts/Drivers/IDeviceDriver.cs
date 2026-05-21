using System;
using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Drivers;

/// <summary>
/// 硬件驱动统一接口
/// </summary>
public interface IDeviceDriver
{
    string DeviceName { get; }
    void Open();
    void Close();

    // 原始数据回调（字节流或原始数组）
    // 为了简化 Demo，我们让驱动直接吐出 double[]
    event Action<int, double[]> RawDataReceived;
    void ApplyChannelSettings(IReadOnlyList<ChannelInfo> settings);
    ChannelInfo? GetChannelSettings(int channelId);
}
