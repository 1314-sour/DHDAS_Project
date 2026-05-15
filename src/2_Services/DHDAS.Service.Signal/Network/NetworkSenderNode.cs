using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using DHDAS.Application.Support;
using MessagePack;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Services;
using DHDAS.Service.Signal.Common;
using Microsoft.Extensions.Logging;

namespace DHDAS.Service.Signal.Network;

public class NetworkSenderNode : BasePipelineNode, INetworkService
{
    public override string NodeId => nameof(NetworkSenderNode);
    private readonly List<NetworkRoute> _routes = new();
    private readonly Dictionary<string, TcpClient> _clients = new();
    private readonly Dictionary<string, NetworkLinkStatus> _statuses = new();
    private readonly Channel<QueuedNetworkPacket> _sendQueue;
    private readonly IDistributedFeedbackService _feedbackService;
    private readonly IWaveformSnapshotService _waveformSnapshotService;
    private readonly IMessenger _messenger;
    private readonly ILogger<NetworkSenderNode> _logger;
    private readonly LinkStatusObservable _linkStatusChanged = new();
    private readonly object _routesLock = new();
    private readonly object _clientsLock = new();
    private readonly object _statusesLock = new();
    private RawDataPacket? _pendingTestPacket;

    public NetworkSenderNode(
        IDistributedFeedbackService feedbackService,
        IWaveformSnapshotService waveformSnapshotService,
        IMessenger messenger,
        ILogger<NetworkSenderNode> logger)
    {
        _feedbackService = feedbackService;
        _waveformSnapshotService = waveformSnapshotService;
        _messenger = messenger;
        _logger = logger;
        _sendQueue = Channel.CreateBounded<QueuedNetworkPacket>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });
    }

    public IObservable<NetworkLinkStatus> LinkStatusChanged => _linkStatusChanged;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var sendWorker = Task.Run(() => RunSendWorkerAsync(ct), ct);
        try
        {
            await base.ExecuteAsync(ct);
        }
        finally
        {
            _sendQueue.Writer.TryComplete();
            await sendWorker;
        }
    }

    public IReadOnlyList<NetworkLinkStatus> GetLinkStatuses()
    {
        lock (_statusesLock)
        {
            return _statuses.Values.ToList();
        }
    }

    public IReadOnlyList<NetworkRoute> GetRoutingTable()
    {
        lock (_routesLock)
        {
            return _routes.ToList();
        }
    }

    // 由应用层插件调用，动态更新路由
    public void UpdateConfig(IEnumerable<NetworkRoute> newRoutes) => UpdateRoutingTable(newRoutes.ToList());

    public void UpdateRoutingTable(List<NetworkRoute> newRoutes)
    {
        int routeCount;
        lock (_routesLock)
        {
            _routes.Clear();
            _routes.AddRange(newRoutes);
            routeCount = _routes.Count;
        }

        foreach (var route in newRoutes)
        {
            PublishStatus(route, IsConnected(route), string.Empty);
        }

        _logger.LogInformation("网络路由已更新，规则数: {Count}", routeCount);
        _feedbackService.Publish(
            "发送端路由已启动",
            $"当前路由规则数: {routeCount}",
            "Info");
    }

    public async Task ConnectAsync(NetworkRoute route, CancellationToken ct = default)
    {
        var client = await GetOrCreateClientAsync(route, ct);
        PublishStatus(route, client is { Connected: true }, client is { Connected: true } ? string.Empty : "连接失败");
    }

    public Task DisconnectAsync(NetworkRoute route, CancellationToken ct = default)
    {
        lock (_clientsLock)
        {
            if (_clients.Remove(route.Endpoint, out var client))
            {
                client.Dispose();
            }
        }

        PublishStatus(route, false, "用户断开连接");
        _logger.LogInformation("网络链路已断开: {NodeName} {Endpoint}", route.NodeName, route.Endpoint);
        return Task.CompletedTask;
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
            $"已生成 CH{channelId} 的 1000 点正弦波，请在“实时波形显示”查看，确认后再发送。",
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

        var packet = _pendingTestPacket.Value;
        using var refBuffer = new RefCountBuffer<RawDataPacket>(packet, _ => { });
        refBuffer.Retain();
        EnqueuePacket(refBuffer, reportNoRoute: true);
    }

    public void SendCurrentTestPacketToRoute(NetworkRoute route)
    {
        if (_pendingTestPacket == null)
        {
            _feedbackService.Publish(
                "没有可发送数据",
                "请先生成测试正弦波，并在“实时波形显示”确认。",
                "Warning");
            return;
        }

        var packet = _pendingTestPacket.Value;
        if (!route.ContainsChannel(packet.ChannelId))
        {
            _feedbackService.Publish(
                "通道与路由不匹配",
                $"当前波形为 CH{packet.ChannelId}，选中路由负责 CH{route.StartChannelId}-{route.EndChannelId}。请先生成选中路由范围内的测试通道。",
                "Warning");
            return;
        }

        using var refBuffer = new RefCountBuffer<RawDataPacket>(packet, _ => { });
        refBuffer.Retain();
        EnqueuePacketToRoute(refBuffer, route);
    }

    protected override bool OnProcess(RefCountBuffer<RawDataPacket> refBuffer)
    {
        EnqueuePacket(refBuffer, reportNoRoute: false);
        return true; // 继续流向本地下一节点
    }

    protected override async Task RunAsSourceAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    private void EnqueuePacket(RefCountBuffer<RawDataPacket> refBuffer, bool reportNoRoute)
    {
        var packet = refBuffer.Data;
        List<NetworkRoute> targets;
        lock (_routesLock)
        {
            targets = _routes.Where(r => r.ContainsChannel(packet.ChannelId)).ToList();
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

        var packetId = Guid.NewGuid();
        foreach (var route in targets)
        {
            TryQueueRoute(refBuffer, route, packetId);
        }
    }

    private void EnqueuePacketToRoute(RefCountBuffer<RawDataPacket> refBuffer, NetworkRoute route)
    {
        TryQueueRoute(refBuffer, route, Guid.NewGuid());
    }

    private void TryQueueRoute(RefCountBuffer<RawDataPacket> refBuffer, NetworkRoute route, Guid packetId)
    {
        refBuffer.Retain();
        if (!_sendQueue.Writer.TryWrite(new QueuedNetworkPacket(route, refBuffer, packetId)))
        {
            refBuffer.Dispose();
            MarkFailure(route, "发送队列不可用，请确认发送端后台节点仍在运行");
        }
    }

    private async Task RunSendWorkerAsync(CancellationToken ct)
    {
        await foreach (var item in _sendQueue.Reader.ReadAllAsync(ct))
        {
            using (item.Buffer)
            {
                await SendToRouteAsync(item.Route, item.Buffer.Data, item.PacketId, ct);
            }
        }
    }

    private async Task SendToRouteAsync(NetworkRoute route, RawDataPacket packet, Guid packetId, CancellationToken ct)
    {
        try
        {
            var client = await GetOrCreateClientAsync(route, ct);
            if (client is not { Connected: true })
            {
                MarkFailure(route, "无法连接目标节点");
                return;
            }

            var netPacket = new NetworkDataPacket
            {
                PacketId = packetId,
                Timestamp = packet.Timestamp,
                ChannelId = packet.ChannelId,
                Data = packet.Data.Take(packet.ActualLength).ToArray(),
                ActualLength = packet.ActualLength,
                SampleRate = packet.SampleRate,
                SourceNode = Environment.MachineName
            };

            lock (client)
            {
                var stream = client.GetStream();
                WriteFrame(stream, netPacket);
                var ack = ReadFrame<NetworkAckPacket>(stream);
                if (ack.Success)
                {
                    MarkAck(route, netPacket);
                    _feedbackService.Publish(
                        "发送端确认成功",
                        $"通道 {ack.ChannelId} 已发送到 {route.Endpoint}，接收长度 {ack.ActualLength}",
                        "Success");
                }
                else
                {
                    MarkFailure(route, ack.Message);
                }
            }
        }
        catch (Exception ex)
        {
            MarkFailure(route, ex.Message);
        }
    }

    private async Task<TcpClient?> GetOrCreateClientAsync(NetworkRoute route, CancellationToken ct)
    {
        lock (_clientsLock)
        {
            if (_clients.TryGetValue(route.Endpoint, out var client) && client.Connected)
            {
                return client;
            }
        }

        try
        {
            var newClient = new TcpClient
            {
                ReceiveTimeout = 2000,
                SendTimeout = 2000
            };

            await newClient.ConnectAsync(route.TargetIp, route.Port);
            lock (_clientsLock)
            {
                _clients[route.Endpoint] = newClient;
            }

            _logger.LogInformation("已连接网络节点: {NodeName} {Endpoint}", route.NodeName, route.Endpoint);
            PublishStatus(route, true, string.Empty);
            return newClient;
        }
        catch (Exception ex)
        {
            PublishNetworkFault(route, ex.Message);
            return null;
        }
    }

    private bool IsConnected(NetworkRoute route)
    {
        lock (_clientsLock)
        {
            return _clients.TryGetValue(route.Endpoint, out var client) && client.Connected;
        }
    }

    private void MarkAck(NetworkRoute route, NetworkDataPacket packet)
    {
        NetworkLinkStatus status;
        lock (_statusesLock)
        {
            status = GetOrCreateStatus(route);
            status.IsConnected = true;
            status.SentPackets++;
            status.AckPackets++;
            status.Mbps += packet.ActualLength * sizeof(double) * 8 / 1_000_000.0;
            status.PacketLossRate = CalculateLoss(status);
            status.LastError = string.Empty;
            status = CloneStatus(status);
        }

        _logger.LogInformation("网络包发送成功: {Endpoint}, Channel={ChannelId}, Length={Length}", route.Endpoint, packet.ChannelId, packet.ActualLength);
        _linkStatusChanged.Publish(status);
    }

    private void MarkFailure(NetworkRoute route, string error)
    {
        NetworkLinkStatus status;
        lock (_statusesLock)
        {
            status = GetOrCreateStatus(route);
            status.FailedPackets++;
            status.PacketLossRate = CalculateLoss(status);
            status.LastError = error;
            status = CloneStatus(status);
        }

        PublishNetworkFault(route, error);
        _linkStatusChanged.Publish(status);
    }

    private void PublishStatus(NetworkRoute route, bool isConnected, string lastError)
    {
        NetworkLinkStatus status;
        lock (_statusesLock)
        {
            status = GetOrCreateStatus(route);
            status.IsConnected = isConnected;
            status.LastError = lastError;
            status = CloneStatus(status);
        }

        _linkStatusChanged.Publish(status);
    }

    private void PublishNetworkFault(NetworkRoute route, string error)
    {
        _logger.LogWarning("网络异常: {NodeName} {Endpoint}, {Error}", route.NodeName, route.Endpoint, error);
        _feedbackService.Publish("网络异常", $"{route.NodeName}({route.Endpoint}): {error}", "Error");
        _messenger.Send(new NetworkFaultMessage(route.NodeName, route.Endpoint, error, DateTime.Now));
    }

    private NetworkLinkStatus GetOrCreateStatus(NetworkRoute route)
    {
        if (!_statuses.TryGetValue(route.Endpoint, out var status))
        {
            status = new NetworkLinkStatus
            {
                NodeName = route.NodeName,
                Endpoint = route.Endpoint
            };
            _statuses[route.Endpoint] = status;
        }

        return status;
    }

    private static double CalculateLoss(NetworkLinkStatus status)
    {
        var total = status.AckPackets + status.FailedPackets;
        return total == 0 ? 0 : status.FailedPackets * 100.0 / total;
    }

    private static NetworkLinkStatus CloneStatus(NetworkLinkStatus status)
    {
        return new NetworkLinkStatus
        {
            NodeName = status.NodeName,
            Endpoint = status.Endpoint,
            IsConnected = status.IsConnected,
            Mbps = status.Mbps,
            SentPackets = status.SentPackets,
            AckPackets = status.AckPackets,
            FailedPackets = status.FailedPackets,
            PacketLossRate = status.PacketLossRate,
            LastError = status.LastError
        };
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
        lock (_clientsLock)
        {
            foreach (var client in _clients.Values) client.Dispose();
            _clients.Clear();
        }

        return Task.CompletedTask;
    }

    private sealed record QueuedNetworkPacket(NetworkRoute Route, RefCountBuffer<RawDataPacket> Buffer, Guid PacketId);

    private sealed class LinkStatusObservable : IObservable<NetworkLinkStatus>
    {
        private readonly object _lock = new();
        private readonly List<IObserver<NetworkLinkStatus>> _observers = new();

        public IDisposable Subscribe(IObserver<NetworkLinkStatus> observer)
        {
            lock (_lock)
            {
                _observers.Add(observer);
            }

            return new Subscription(() =>
            {
                lock (_lock)
                {
                    _observers.Remove(observer);
                }
            });
        }

        public void Publish(NetworkLinkStatus status)
        {
            List<IObserver<NetworkLinkStatus>> observers;
            lock (_lock)
            {
                observers = _observers.ToList();
            }

            foreach (var observer in observers)
            {
                observer.OnNext(status);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _dispose;
            private int _disposed;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _dispose();
                }
            }
        }
    }
}
