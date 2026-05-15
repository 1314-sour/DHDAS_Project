using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal;

public class StorageService : BasePipelineNode
{
    public override string NodeId => nameof(StorageService);

    private FileStream? _fileStream;
    private BinaryWriter? _writer;
    private readonly string _filePath;

    // 控制开关：实际应用中可以通过接口开启/关闭
    public bool IsRecording { get; set; } = true;

    public StorageService(DistributedRuntimeOptions runtimeOptions)
    {
        // Demo 期间，直接存在程序运行目录下
        var suffix = runtimeOptions.IsReceiver ? $"_{runtimeOptions.ListenPort}" : string.Empty;
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"recorded_data{suffix}.dat");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[系统] 存储服务启动，目标文件: {_filePath}");

        // 初始化文件流：使用同步或异步写入取决于性能需求
        // 这里使用较大的缓冲区(64KB)来优化磁盘IO性能
        _fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536);
        _writer = new BinaryWriter(_fileStream);

        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// 处理流经该节点的数据包
    /// </summary>
    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        if (_writer == null) return true;

        try
        {
            var packet = refBuffer.Data;

            // --- 简单的二进制协议定义 ---
            // 1. 写入包头 (可选，Demo简化)
            _writer.Write(packet.Timestamp);    // 8 bytes
            _writer.Write(packet.ChannelId);    // 4 bytes
            _writer.Write(packet.SampleRate);   // 8 bytes
            _writer.Write(packet.ActualLength); // 4 bytes

            // 2. 写入原始数据数组
            for (int i = 0; i < packet.ActualLength; i++)
            {
                _writer.Write(packet.Data[i]);
            }

            // 为了性能，不建议在这里 Flush，让操作系统决定写入时机
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[存储] 写入异常: {ex.Message}");
            return false;
        }
    }

    // 在这里执行真正的文件关闭，确保此时已经没有人在调用 OnProcess 了
    protected override Task OnCleanupAsync()
    {
        Console.WriteLine("[系统] 存储服务正在执行最后的 Flush 并关闭文件...");
        _writer?.Flush();
        _writer?.Dispose();
        _fileStream?.Dispose();
        return Task.CompletedTask;
    }
}
