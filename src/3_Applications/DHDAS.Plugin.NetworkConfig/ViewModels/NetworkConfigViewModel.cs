using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;

namespace DHDAS.Plugin.NetworkConfig.ViewModels;

public class NetworkConfigViewModel : PluginViewModelBase
{
    private readonly INetworkService _networkService;
    private readonly DistributedRuntimeOptions _runtimeOptions;
    private readonly IDisposable _statusSubscription;

    public ObservableCollection<NetworkRoute> Routes { get; } = new();
    public ObservableCollection<NetworkLinkStatus> LinkStatuses { get; } = new();

    [Reactive] public string InputNodeName { get; set; } = "本地回环节点";
    [Reactive] public string InputIp { get; set; } = "127.0.0.1";
    [Reactive] public int InputPort { get; set; } = 5000;
    [Reactive] public int StartChannelId { get; set; } = 0;
    [Reactive] public int EndChannelId { get; set; } = 0;
    [Reactive] public int TestChannelId { get; set; } = 0;

    public bool IsSender => _runtimeOptions.IsSender;
    public bool IsReceiver => _runtimeOptions.IsReceiver;
    public string RoleText => _runtimeOptions.IsSender ? "发送端模式" : "接收端模式";
    public string ReceiverText => $"接收端正在监听 {_runtimeOptions.ListenPort} 端口。收到数据后，请切换到“实时波形显示”查看完整曲线。";

    public NetworkConfigViewModel(
        INetworkService networkService,
        DistributedRuntimeOptions runtimeOptions,
        IMessenger messenger) : base()
    {
        _networkService = networkService;
        _runtimeOptions = runtimeOptions;
        InputIp = runtimeOptions.TargetIp;
        InputPort = runtimeOptions.TargetPort;
        StartChannelId = runtimeOptions.ChannelId;
        EndChannelId = runtimeOptions.ChannelId;
        TestChannelId = runtimeOptions.ChannelId;
        _statusSubscription = _networkService.LinkStatusChanged.Subscribe(new LinkStatusObserver(UpdateLinkStatus));
    }

    public void AddRoute()
    {
        var route = new NetworkRoute(InputNodeName, InputIp, InputPort, StartChannelId, EndChannelId);
        Routes.Add(route);
        ApplyRoutingTable();
    }

    public void ApplyRoutingTable()
    {
        _networkService.UpdateRoutingTable(Routes.ToList());
    }

    public async void ConnectSelected()
    {
        var route = Routes.FirstOrDefault();
        if (route != null)
        {
            await _networkService.ConnectAsync(route);
        }
    }

    public async void DisconnectSelected()
    {
        var route = Routes.FirstOrDefault();
        if (route != null)
        {
            await _networkService.DisconnectAsync(route);
        }
    }

    public void SendOnce()
    {
        _networkService.SendCurrentTestPacket();
    }

    public void GenerateWaveform()
    {
        _networkService.GenerateTestSinePacket(TestChannelId);
    }

    public override void OnDeactivated()
    {
        _statusSubscription.Dispose();
        base.OnDeactivated();
    }

    private void UpdateLinkStatus(NetworkLinkStatus status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = LinkStatuses.FirstOrDefault(s => s.Endpoint == status.Endpoint);
            if (existing == null)
            {
                LinkStatuses.Add(status);
                return;
            }

            var index = LinkStatuses.IndexOf(existing);
            LinkStatuses[index] = status;
        });
    }

    private sealed class LinkStatusObserver : IObserver<NetworkLinkStatus>
    {
        private readonly Action<NetworkLinkStatus> _onNext;

        public LinkStatusObserver(Action<NetworkLinkStatus> onNext) => _onNext = onNext;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(NetworkLinkStatus value) => _onNext(value);
    }
}
