using System;
using System.Threading;
using System.Threading.Tasks;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Drivers;
using DHDAS.Contracts.Memory;
using DHDAS.Infrastructure.Core.Session;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal;

public class AcquisitionService : BasePipelineNode
{
    private readonly IDeviceDriver _driver;
    private readonly SessionManager _sessionManager;

    public override string NodeId => nameof(AcquisitionService);

    public AcquisitionService(
       IDeviceDriver driver,
       SessionManager sessionManager)
    {
        _driver = driver;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 实现源节点特有的生产逻辑
    /// </summary>
    protected override async Task RunAsSourceAsync(CancellationToken ct)
    {
        Console.WriteLine($"[系统] 节点 {NodeId} 正在启动硬件驱动...");

        _driver.RawDataReceived += (channelId, data) =>
        {
            // 1. 定位 Session
            var session = _sessionManager.GetSession(channelId);

            // 2. 从 Session 的私有池借用内存并拷贝
            double[] poolArray = session.Rent(data.Length);
            Array.Copy(data, poolArray, data.Length);

            // 3. 封装标准包
            var packet = new RawDataPacket
            {
                ChannelId = channelId,
                Data = poolArray,
                ActualLength = data.Length,
                Timestamp = DateTime.Now.Ticks
            };

            // 4. 创建引用计数包裹，定义回收行为
            var refBuffer = new RefCountBuffer<RawDataPacket>(packet, p => session.Return(p.Data));

            // 5. 初始引用计数（代表当前采集回调持有的那份所有权）
            refBuffer.Retain();

            // 6. 如果编排器连了出水管，就往下一站发
            if (_outputPipe != null)
            {
                // 为下一站增加一次计数
                refBuffer.Retain();
                if (!_outputPipe.TryWrite(refBuffer))
                {
                    // 若管道满导致写入失败，必须手动释放刚才为管道加的计数
                    refBuffer.Dispose();
                }
            }

            // 7. 释放本回调函数持有的引用（此时若没有下一站，内存会立即回池）
            refBuffer.Dispose();
        };

        // 开启硬件
        _driver.Open();

        // 挂起线程，直到系统通知停止
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[系统] 节点 {NodeId} 正在关闭硬件驱动...");
            _driver.Close();
        }
    }

    /// <summary>
    /// 由于是源节点，OnProcess 不会被 BasePipelineNode 调用
    /// </summary>
    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer) => true;
}