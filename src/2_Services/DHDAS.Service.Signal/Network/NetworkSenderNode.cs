using System.Buffers.Binary;
using System.Net.Sockets;
using MessagePack;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Services;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal.Network;

public class NetworkSenderNode : BasePipelineNode
{
    public override string NodeId => nameof(NetworkSenderNode);
    private readonly List<NetworkRoute> _routes = new();
    private readonly Dictionary<string, TcpClient> _clients = new();
    private readonly IDistributedFeedbackService _feedbackService;
    private readonly IWaveformSnapshotService _waveformSnapshotService;
    private readonly object _routesLock = new();
    private RawDataPacket? _pendingTestPacket;

    public NetworkSenderNode(
        IDistributedFeedbackService feedbackService,
        IWaveformSnapshotService waveformSnapshotService)
    {
        _feedbackService = feedbackService;
        _waveformSnapshotService = waveformSnapshotService;
    }

    // 由应用层插件调用，动态更新路由
    public void UpdateConfig(IEnumerable<NetworkRoute> newRoutes)
    {
        int routeCount;
        lock (_routesLock)
        {
            _routes.Clear();
            _routes.AddRange(newRoutes);
            routeCount = _routes.Count;
        }

        _feedbackService.Publish(
            "发送端路由已启动",
            $"当前路由规则数: {routeCount}",
            "Info");
    }

    public void GenerateTestSinePacket(int channelId)
    {
        const int sampleRate = 1000;
        const int batchSize = 1000;
        const double frequency = 5;

        var data = new double[batchSize];
        for (int i = 0; i < batchSize; i++)
        {
            data[i] = Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }

        var packet = new RawDataPacket
        {
            Timestamp = DateTime.Now.Ticks,
            ChannelId = channelId,
            SampleRate = sampleRate,
            Data = data,
            ActualLength = data.Length
        };

        _pendingTestPacket = packet;
        _waveformSnapshotService.Publish(new WaveformSnapshot
        {
            ChannelId = packet.ChannelId,
            Timestamp = packet.Timestamp,
            Samples = packet.Data.Take(packet.ActualLength).ToArray(),
            ActualLength = packet.ActualLength
        });

        _feedbackService.Publish(
            "发送端已生成波形",
            $"已生成通道 {channelId} 的 1000 点正弦波，请在“实时波形显示”查看，确认后再发送。",
            "Success");
    }

    public void SendCurrentTestPacket()
    {
        if (_pendingTestPacket == null)
        {
            _feedbackService.Publish(
                "没有可发送数据",
                "请先生成测试正弦波，并在“实时波形显示”确认。",
                "Warning");
            return;
        }

        SendPacket(_pendingTestPacket.Value, reportNoRoute: true);
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        SendPacket(refBuffer.Data, reportNoRoute: false);
        return true; // 继续流向本地下一节点
    }

    private void SendPacket(RawDataPacket packet, bool reportNoRoute)
    {
        List<NetworkRoute> targets;
        lock (_routesLock)
        {
            targets = _routes.Where(r => r.ChannelId == packet.ChannelId).ToList();
        }

        if (!targets.Any())
        {
            if (reportNoRoute)
            {
                _feedbackService.Publish(
                    "未配置发送路由",
                    $"请先为通道 {packet.ChannelId} 添加目标路由。",
                    "Warning");
            }

            return;
        }

        foreach (var route in targets)
        {
            try
            {
                var client = GetOrCreateClient(route.TargetIp, route.Port);
                if (client is not { Connected: true })
                {
                    _feedbackService.Publish(
                        "发送失败",
                        $"无法连接 {route.TargetIp}:{route.Port}",
                        "Error");
                    continue;
                }

                var netPacket = new NetworkDataPacket
                {
                    Timestamp = packet.Timestamp,
                    ChannelId = packet.ChannelId,
                    Data = packet.Data,
                    ActualLength = packet.ActualLength
                };

                lock (client)
                {
                    var stream = client.GetStream();

                    WriteFrame(stream, netPacket);
                    var ack = ReadFrame<NetworkAckPacket>(stream);
                    if (ack.Success)
                    {
                        _feedbackService.Publish(
                            "发送端确认成功",
                            $"通道 {ack.ChannelId} 已发送到 {route.TargetIp}:{route.Port}，接收长度 {ack.ActualLength}",
                            "Success");
                    }
                    else
                    {
                        _feedbackService.Publish(
                            "接收端拒收",
                            $"通道 {packet.ChannelId} -> {route.TargetIp}:{route.Port}，原因: {ack.Message}",
                            "Warning");
                    }
                }
            }
            catch (Exception ex)
            {
                _feedbackService.Publish(
                    "发送异常",
                    $"发送至 {route.TargetIp}:{route.Port} 失败: {ex.Message}",
                    "Error");
            }
        }
    }

    private TcpClient? GetOrCreateClient(string ip, int port)
    {
        string key = $"{ip}:{port}";
        if (_clients.TryGetValue(key, out var client) && client.Connected) return client;

        try
        {
            var newClient = new TcpClient();
            newClient.Connect(ip, port);
            newClient.ReceiveTimeout = 2000;
            newClient.SendTimeout = 2000;
            _clients[key] = newClient;
            _feedbackService.Publish("发送端已连接", $"已连接接收端 {key}", "Success");
            return newClient;
        }
        catch (Exception ex)
        {
            _feedbackService.Publish("连接接收端失败", $"{key}: {ex.Message}", "Error");
            return null;
        }
    }

    private static void WriteFrame<T>(NetworkStream stream, T value)
    {
        var payload = MessagePackSerializer.Serialize(value);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        stream.Write(header, 0, header.Length);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    private static T ReadFrame<T>(NetworkStream stream)
    {
        var header = ReadExact(stream, 4);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > 10 * 1024 * 1024)
        {
            throw new InvalidDataException($"无效消息长度: {length}");
        }

        var payload = ReadExact(stream, length);
        return MessagePackSerializer.Deserialize<T>(payload);
    }

    private static byte[] ReadExact(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = stream.Read(buffer, offset, length - offset);
            if (read == 0)
            {
                throw new EndOfStreamException("连接已关闭。");
            }

            offset += read;
        }

        return buffer;
    }

    protected override Task OnCleanupAsync()
    {
        foreach (var client in _clients.Values) client.Dispose();
        return Task.CompletedTask;
    }
}
