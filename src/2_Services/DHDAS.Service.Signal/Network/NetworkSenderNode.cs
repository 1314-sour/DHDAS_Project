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
    private readonly HashSet<string> _reportedSendSuccessRoutes = new();
    private readonly IDistributedFeedbackService _feedbackService;
    private readonly object _routesLock = new();

    public NetworkSenderNode(IDistributedFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
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

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        var packet = refBuffer.Data;
        List<NetworkRoute> targets;
        lock (_routesLock)
        {
            targets = _routes.Where(r => r.ChannelId == packet.ChannelId).ToList();
        }

        if (!targets.Any()) return true; // 快速检查：如果该通道没有配置路由，直接放行

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

                    // 序列化并发送，然后等待接收端业务确认
                    MessagePackSerializer.Serialize(stream, netPacket);
                    stream.Flush();

                    var ack = MessagePackSerializer.Deserialize<NetworkAckPacket>(stream);
                    if (ack.Success)
                    {
                        var successKey = $"{route.TargetIp}:{route.Port}:{ack.ChannelId}";
                        if (_reportedSendSuccessRoutes.Add(successKey))
                        {
                            _feedbackService.Publish(
                                "发送端确认成功",
                                $"通道 {ack.ChannelId} 已发送到 {route.TargetIp}:{route.Port}，接收长度 {ack.ActualLength}",
                                "Success");
                        }
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
        return true; // 继续流向本地下一节点
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

    protected override Task OnCleanupAsync()
    {
        foreach (var client in _clients.Values) client.Dispose();
        return Task.CompletedTask;
    }
}
