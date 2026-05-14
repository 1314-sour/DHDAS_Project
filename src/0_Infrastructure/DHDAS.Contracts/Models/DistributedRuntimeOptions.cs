namespace DHDAS.Contracts.Models;

public class DistributedRuntimeOptions
{
    public string Role { get; set; } = "receiver";
    public string TargetIp { get; set; } = "127.0.0.1";
    public int TargetPort { get; set; } = 5000;
    public int ChannelId { get; set; }

    public bool IsSender => string.Equals(Role, "sender", StringComparison.OrdinalIgnoreCase);
    public bool IsReceiver => string.Equals(Role, "receiver", StringComparison.OrdinalIgnoreCase);
}
