namespace DHDAS.Contracts.Models;

public class NetworkLinkStatus
{
    public string NodeName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public double Mbps { get; set; }
    public long SentPackets { get; set; }
    public long AckPackets { get; set; }
    public long FailedPackets { get; set; }
    public double PacketLossRate { get; set; }
    public string LastError { get; set; } = string.Empty;

    public override string ToString()
    {
        var state = IsConnected ? "已连接" : "未连接";
        return $"{NodeName} | {Endpoint} | {state} | {Mbps:F3} MBps | 成功 {AckPackets} | 失败 {FailedPackets} | 丢包 {PacketLossRate:F1}% | {LastError}";
    }
}
