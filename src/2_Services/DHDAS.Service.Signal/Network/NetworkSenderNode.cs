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

    // 由应用层插件调用，动态更新路由
    public void UpdateConfig(IEnumerable<NetworkRoute> newRoutes)
    {
        _routes.Clear();
        _routes.AddRange(newRoutes);
        Console.WriteLine($"[网络] 路由表已更新，当前规则数: {_routes.Count}");
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        var packet = refBuffer.Data;
        var targets = _routes.Where(r => r.ChannelId == packet.ChannelId);

        if (!targets.Any()) return true; // 快速检查：如果该通道没有配置路由，直接放行

        foreach (var route in targets)
        {
            try
            {
                var client = GetOrCreateClient(route.TargetIp, route.Port);
                if (client is { Connected: true })
                {
                    var netPacket = new NetworkDataPacket
                    {
                        Timestamp = packet.Timestamp,
                        ChannelId = packet.ChannelId,
                        Data = packet.Data,
                        ActualLength = packet.ActualLength
                    };

                    // 序列化并发送
                    MessagePackSerializer.Serialize(client.GetStream(), netPacket);
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
            _clients[key] = newClient;
            return newClient;
        }
        catch { return null; }
    }

    protected override Task OnCleanupAsync()
    {
        foreach (var client in _clients.Values) client.Dispose();
        return Task.CompletedTask;
    }
}