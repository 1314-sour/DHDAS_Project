using MessagePack;

namespace DHDAS.Contracts.Models;

[MessagePackObject]
public class NetworkAckPacket
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public long Timestamp { get; set; }
    [Key(2)] public int ChannelId { get; set; }
    [Key(3)] public int ActualLength { get; set; }
    [Key(4)] public string Message { get; set; } = string.Empty;
}
