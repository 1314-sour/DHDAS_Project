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

    public ObservableCollection<NetworkRoute> Routes { get; } = new();

    [Reactive] public string InputIp { get; set; } = "127.0.0.1";
    [Reactive] public int InputChannel { get; set; } = 0;

    public NetworkConfigViewModel(NetworkSenderNode senderNode, IMessenger messenger) : base()
    {
        _senderNode = senderNode;
    }

    public void AddRoute()
    {
        var route = new NetworkRoute(InputChannel, InputIp, 5000);
        Routes.Add(route);
        _senderNode.UpdateConfig(Routes);
    }
}