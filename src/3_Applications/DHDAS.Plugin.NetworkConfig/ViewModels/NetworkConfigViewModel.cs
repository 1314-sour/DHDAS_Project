using System.Collections.ObjectModel;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Service.Signal.Network;
using ReactiveUI;

namespace DHDAS.Plugin.NetworkConfig.ViewModels;

public class NetworkConfigViewModel : PluginViewModelBase
{
    private readonly NetworkSenderNode _senderNode;
    private string _inputIp = "127.0.0.1";
    private int _inputChannel;

    public ObservableCollection<NetworkRoute> Routes { get; } = new();

    public string InputIp
    {
        get => _inputIp;
        set => this.RaiseAndSetIfChanged(ref _inputIp, value);
    }

    public int InputChannel
    {
        get => _inputChannel;
        set => this.RaiseAndSetIfChanged(ref _inputChannel, value);
    }

    public NetworkConfigViewModel(NetworkSenderNode senderNode, IMessenger messenger)
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
