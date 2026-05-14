using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Service.Signal.Network;

namespace DHDAS.Plugin.NetworkConfig.ViewModels;

public class NetworkConfigViewModel : PluginViewModelBase
{
    private readonly NetworkSenderNode _senderNode;
    private readonly DistributedRuntimeOptions _runtimeOptions;

    public ObservableCollection<NetworkRoute> Routes { get; } = new();

    [Reactive] public string InputIp { get; set; } = "127.0.0.1";
    [Reactive] public int InputChannel { get; set; } = 0;

    public bool IsSender => _runtimeOptions.IsSender;
    public bool IsReceiver => _runtimeOptions.IsReceiver;
    public string RoleText => _runtimeOptions.IsSender ? "发送端模式" : "接收端模式";
    public string ReceiverText => "接收端正在等待发送端数据，收到后会弹窗展示通道、长度和采样值预览。";

    public NetworkConfigViewModel(
        NetworkSenderNode senderNode,
        DistributedRuntimeOptions runtimeOptions,
        IMessenger messenger) : base()
    {
        _senderNode = senderNode;
        _runtimeOptions = runtimeOptions;
        InputIp = runtimeOptions.TargetIp;
        InputChannel = runtimeOptions.ChannelId;
    }

    public void AddRoute()
    {
        var route = new NetworkRoute(InputChannel, InputIp, 5000);
        Routes.Add(route);
        _senderNode.UpdateConfig(Routes);
    }

    public void SendOnce()
    {
        _senderNode.SendTestSinePacket(InputChannel);
    }
}
