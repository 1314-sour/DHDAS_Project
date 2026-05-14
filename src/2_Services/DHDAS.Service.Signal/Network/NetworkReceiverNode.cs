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
    private readonly HashSet<int> _reportedReceivedChannels = new();
    private TcpListener? _listener;

    public NetworkReceiverNode(SessionManager sessionManager, IDistributedFeedbackService feedbackService)
    {
        _sessionManager = sessionManager;
        _feedbackService = feedbackService;
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
                // 反序列化接收
                var netPacket = await MessagePackSerializer.DeserializeAsync<NetworkDataPacket>(stream);

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
                    if (_reportedReceivedChannels.Add(rawPacket.ChannelId))
                    {
                        _feedbackService.Publish(
                            "接收端确认成功",
                            $"已正确接收通道 {rawPacket.ChannelId}，长度 {rawPacket.ActualLength}",
                            "Success");
                    }

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

        await MessagePackSerializer.SerializeAsync(stream, ack, cancellationToken: ct);
        await stream.FlushAsync(ct);
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer) => true;
}
