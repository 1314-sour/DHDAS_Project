using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using MessagePack;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Services;
using DHDAS.Infrastructure.Core.Session;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal.Network;

public class NetworkReceiverNode : BasePipelineNode
{
    public override string NodeId => nameof(NetworkReceiverNode);
    private readonly SessionManager _sessionManager;
    private readonly IDistributedFeedbackService _feedbackService;
    private readonly IWaveformSnapshotService _waveformSnapshotService;
    private TcpListener? _listener;

    public NetworkReceiverNode(
        SessionManager sessionManager,
        IDistributedFeedbackService feedbackService,
        IWaveformSnapshotService waveformSnapshotService)
    {
        _sessionManager = sessionManager;
        _feedbackService = feedbackService;
        _waveformSnapshotService = waveformSnapshotService;
    }

    protected override async Task RunAsSourceAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, 5000);
        _listener.Start();
        _feedbackService.Publish("接收端已启动", "正在监听 5000 端口", "Success");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, ct);
            }
        }
        finally { _listener.Stop(); }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        while (!ct.IsCancellationRequested && client.Connected)
        {
            try
            {
                var netPacket = await ReadFrameAsync<NetworkDataPacket>(stream, ct);

                if (netPacket.Data.Length < netPacket.ActualLength)
                {
                    _feedbackService.Publish(
                        "接收数据异常",
                        $"通道 {netPacket.ChannelId} 数据长度小于有效长度",
                        "Error");
                    await SendAckAsync(stream, false, netPacket, "数据长度小于有效长度", ct);
                    continue;
                }

                // 还原 RefCountBuffer
                var session = _sessionManager.GetSession(netPacket.ChannelId);
                double[] poolArray = session.Rent(netPacket.ActualLength);
                Array.Copy(netPacket.Data, poolArray, netPacket.ActualLength);

                var rawPacket = new RawDataPacket
                {
                    ChannelId = netPacket.ChannelId,
                    Data = poolArray,
                    ActualLength = netPacket.ActualLength,
                    Timestamp = netPacket.Timestamp
                };

                var refBuffer = new RefCountBuffer<RawDataPacket>(rawPacket, p => session.Return(p.Data));
                refBuffer.Retain(); // 初始计数
                var acceptedByPipeline = true;
                if (_outputPipe != null)
                {
                    refBuffer.Retain(); // 给管道
                    if (!_outputPipe.TryWrite(refBuffer))
                    {
                        refBuffer.Dispose();
                        acceptedByPipeline = false;
                    }
                }
                refBuffer.Dispose(); // 本函数释放

                if (acceptedByPipeline)
                {
                    _waveformSnapshotService.Publish(new WaveformSnapshot
                    {
                        ChannelId = rawPacket.ChannelId,
                        Timestamp = rawPacket.Timestamp,
                        Samples = rawPacket.Data.Take(rawPacket.ActualLength).ToArray(),
                        ActualLength = rawPacket.ActualLength
                    });

                    _feedbackService.Publish(
                        "接收端收到数据",
                        BuildReceivedPreview(rawPacket),
                        "Success");

                    await SendAckAsync(stream, true, netPacket, "OK", ct);
                }
                else
                {
                    _feedbackService.Publish(
                        "接收端管道繁忙",
                        $"通道 {rawPacket.ChannelId} 暂时无法写入本机管道",
                        "Warning");
                    await SendAckAsync(stream, false, netPacket, "接收端本机管道繁忙", ct);
                }
            }
            catch (Exception ex)
            {
                _feedbackService.Publish("接收异常", ex.Message, "Error");
                break;
            }
        }
    }

    private static string BuildReceivedPreview(RawDataPacket packet)
    {
        var previewLength = Math.Min(packet.ActualLength, 8);
        var preview = string.Join(", ", packet.Data.Take(previewLength).Select(v => v.ToString("F3")));
        return $"通道: {packet.ChannelId}\n长度: {packet.ActualLength}\n前 {previewLength} 个采样值: {preview}";
    }

    private static async Task SendAckAsync(NetworkStream stream, bool success, NetworkDataPacket packet, string message, CancellationToken ct)
    {
        var ack = new NetworkAckPacket
        {
            Success = success,
            Timestamp = packet.Timestamp,
            ChannelId = packet.ChannelId,
            ActualLength = packet.ActualLength,
            Message = message
        };

        await WriteFrameAsync(stream, ack, ct);
    }

    private static async Task WriteFrameAsync<T>(NetworkStream stream, T value, CancellationToken ct)
    {
        var payload = MessagePackSerializer.Serialize(value);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, 0, header.Length, ct);
        await stream.WriteAsync(payload, 0, payload.Length, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<T> ReadFrameAsync<T>(NetworkStream stream, CancellationToken ct)
    {
        var header = await ReadExactAsync(stream, 4, ct);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > 10 * 1024 * 1024)
        {
            throw new InvalidDataException($"无效消息长度: {length}");
        }

        var payload = await ReadExactAsync(stream, length, ct);
        return MessagePackSerializer.Deserialize<T>(payload);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken ct)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer, offset, length - offset, ct);
            if (read == 0)
            {
                throw new EndOfStreamException("连接已关闭。");
            }

            offset += read;
        }

        return buffer;
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer) => true;
}
