namespace DHDAS.Contracts.Models;

public class NetworkRoute
{
    public NetworkRoute()
    {
    }

    public NetworkRoute(int channelId, string targetIp, int port)
        : this("本地回环节点", targetIp, port, channelId, channelId, true)
    {
    }

    public NetworkRoute(string nodeName, string targetIp, int port, int startChannelId, int endChannelId, bool isEnabled = true)
    {
        NodeName = nodeName;
        TargetIp = targetIp;
        Port = port;
        StartChannelId = startChannelId;
        EndChannelId = endChannelId;
        IsEnabled = isEnabled;
    }

    public string NodeName { get; set; } = "本地回环节点";
    public string TargetIp { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public int StartChannelId { get; set; }
    public int EndChannelId { get; set; }
    public bool IsEnabled { get; set; } = true;

    public bool ContainsChannel(int channelId)
    {
        return IsEnabled && channelId >= StartChannelId && channelId <= EndChannelId;
    }

    public string Endpoint => $"{TargetIp}:{Port}";

    public override string ToString()
    {
        return $"{NodeName} | {TargetIp}:{Port} | CH{StartChannelId}-{EndChannelId} | {(IsEnabled ? "启用" : "停用")}";
    }
}
