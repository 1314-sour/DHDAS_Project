using System.Net.Sockets;
using MessagePack;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal.Network;

public class NetworkSenderNode : BasePipelineNode
{
    public override string NodeId => nameof(NetworkSenderNode);
    private readonly List<NetworkRoute> _routes = new();
    private readonly Dictionary<string, TcpClient> _clients = new();
    private readonly object _routesLock = new();

    // 由应用层插件调用，动态更新路由
    public void UpdateConfig(IEnumerable<NetworkRoute> newRoutes)
    {
        lock (_routesLock)
        {
            _routes.Clear();
            _routes.AddRange(newRoutes);
            Console.WriteLine($"[网络] 路由表已更新，当前规则数: {_routes.Count}");
        }
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
                    Console.WriteLine($"[网络] 发送失败: 无法连接 {route.TargetIp}:{route.Port}");
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
                        Console.WriteLine($"[网络] 发送成功: 通道 {ack.ChannelId}, 长度 {ack.ActualLength}, 目标 {route.TargetIp}:{route.Port}");
                    }
                    else
                    {
                        Console.WriteLine($"[网络] 接收端拒收: 通道 {packet.ChannelId}, 目标 {route.TargetIp}:{route.Port}, 原因: {ack.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[网络] 发送至 {route.TargetIp} 失败: {ex.Message}");
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
            Console.WriteLine($"[网络] 已连接接收端: {key}");
            return newClient;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[网络] 连接接收端失败: {key}, 原因: {ex.Message}");
            return null;
        }
    }

    protected override Task OnCleanupAsync()
    {
        foreach (var client in _clients.Values) client.Dispose();
        return Task.CompletedTask;
    }
}
