using DHDAS.Application.Support;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Models;
using DHDAS.Service.Signal.Common;
using Microsoft.Extensions.Logging;

namespace DHDAS.Service.Signal;

public sealed class StorageService : BasePipelineNode
{
    private readonly object _syncRoot = new();
    private readonly ILogger<StorageService> _logger;
    private string? _currentFilePath;
    private FileStream? _fileStream;
    private BinaryWriter? _writer;

    public override string NodeId => nameof(StorageService);

    public bool IsRecording { get; set; } = true;

    public StorageService(IMessenger messenger, ILogger<StorageService> logger)
    {
        _logger = logger;

        messenger.Listen<ProjectChangedMessage>()
            .Subscribe(message => HandleProjectChanged(message));
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        if (!IsRecording)
        {
            return true;
        }

        try
        {
            lock (_syncRoot)
            {
                if (_writer == null)
                {
                    return true;
                }

                var packet = refBuffer.Data;
                _writer.Write(packet.Timestamp);
                _writer.Write(packet.ChannelId);
                _writer.Write(packet.SampleRate);
                _writer.Write(packet.ActualLength);

                for (var i = 0; i < packet.ActualLength; i++)
                {
                    _writer.Write(packet.Data[i]);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入原始数据失败");
            return false;
        }
    }

    protected override Task OnCleanupAsync()
    {
        lock (_syncRoot)
        {
            CloseWriter();
        }

        return Task.CompletedTask;
    }

    private void HandleProjectChanged(ProjectChangedMessage message)
    {
        try
        {
            lock (_syncRoot)
            {
                if (message.Kind == ProjectChangeKind.Closed ||
                    message.Kind == ProjectChangeKind.Deleted ||
                    message.Experiment == null)
                {
                    CloseWriter();
                    _currentFilePath = null;
                    _logger.LogInformation("存储目标已关闭");
                    return;
                }

                var targetFilePath = Path.Combine(message.Experiment.Path, "RawData", "recorded_data.dat");
                if (string.Equals(_currentFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SwitchWriter(targetFilePath);
                _logger.LogInformation("存储目标已切换: {TargetFilePath}", targetFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理工程切换消息失败");
        }
    }

    private void SwitchWriter(string targetFilePath)
    {
        CloseWriter();

        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
        _fileStream = new FileStream(
            targetFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 65536);
        _writer = new BinaryWriter(_fileStream);
        _currentFilePath = targetFilePath;
    }

    private void CloseWriter()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _fileStream?.Dispose();
        _writer = null;
        _fileStream = null;
    }
}
