using MessagePack;

namespace DHDAS.Contracts.Models;

[MessagePackObject]
public class NetworkDataPacket
{
    [Key(0)] public long Timestamp { get; set; }
    [Key(1)] public int ChannelId { get; set; }
    [Key(2)] public double[] Data { get; set; } = Array.Empty<double>();
    [Key(3)] public int ActualLength { get; set; }
}