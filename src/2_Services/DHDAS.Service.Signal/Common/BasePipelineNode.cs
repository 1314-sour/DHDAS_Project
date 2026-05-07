using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;

namespace DHDAS.Service.Signal.Common;

// 位于 DHDAS.Service.Signal.Common;
public abstract class BasePipelineNode : BackgroundService, IPipelineNode
{
    public abstract string NodeId { get; }
    protected ChannelReader<RefCountBuffer<RawDataPacket>>? _inputPipe;
    protected ChannelWriter<RefCountBuffer<RawDataPacket>>? _outputPipe;

    public void SetInput(ChannelReader<RefCountBuffer<RawDataPacket>>? reader) => _inputPipe = reader;
    public void SetOutput(ChannelWriter<RefCountBuffer<RawDataPacket>>? writer) => _outputPipe = writer;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (_inputPipe != null)
            {
                // ReadAllAsync 在收到取消信号或管道完成(Complete)后会退出
                await foreach (var refBuffer in _inputPipe.ReadAllAsync(ct))
                {
                    using (refBuffer)
                    {
                        bool shouldForward = OnProcess(refBuffer);
                        if (_outputPipe != null && shouldForward)
                        {
                            refBuffer.Retain();
                            if (!_outputPipe.TryWrite(refBuffer)) refBuffer.Dispose();
                        }
                    }
                }
            }
            else
            {
                await RunAsSourceAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[{NodeId}] 被取消。");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{NodeId}] 运行时异常: {ex.Message}");
        }
        finally
        {
            // 关键：循环结束后，统一执行收尾
            await OnCleanupAsync();
            Console.WriteLine($"[系统] 节点 {NodeId} 已安全退出。");
        }
    }

    // 子类可选实现的清理逻辑
    protected virtual Task OnCleanupAsync() => Task.CompletedTask;

    // 子类实现具体业务：返回 true 代表允许数据流向下一站，false 代表流在此终止
    protected abstract bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer);

    protected virtual Task RunAsSourceAsync(CancellationToken ct) => Task.CompletedTask;
}