using System.Net;
using System.Net.Sockets;
using MessagePack;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Infrastructure.Core.Session;
using DHDAS.Service.Signal.Common;

namespace DHDAS.Service.Signal.Network;

public class NetworkReceiverNode : BasePipelineNode
{
    public override string NodeId => nameof(NetworkReceiverNode);
    private readonly SessionManager _sessionManager;
    private TcpListener? _listener;

    public NetworkReceiverNode(SessionManager sessionManager) => _sessionManager = sessionManager;

    protected override async Task RunAsSourceAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, 5000);
        _listener.Start();
        Console.WriteLine("[网络] 接收节点已在端口 5000 启动监听...");

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
            Console.WriteLine("[网络] 收到新连接，准备接收数据...");
            try
            {
                // 反序列化接收
                var netPacket = await MessagePackSerializer.DeserializeAsync<NetworkDataPacket>(stream);

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
                Console.WriteLine($"[网络] 成功接收数据包: 通道 {rawPacket.ChannelId}, 长度 {rawPacket.ActualLength}");
                if (_outputPipe != null)
                {
                    refBuffer.Retain(); // 给管道
                    if (!_outputPipe.TryWrite(refBuffer)) refBuffer.Dispose();
                }
                refBuffer.Dispose(); // 本函数释放
            }
            catch
            {
                Console.WriteLine("[网络] 接收数据包时发生错误");
                break;
            }
        }

        Console.WriteLine("[网络] 连接已关闭");
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer) => true;
}