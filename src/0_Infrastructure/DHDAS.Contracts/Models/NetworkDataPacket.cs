using MessagePack;

namespace DHDAS.Contracts.Models;

[MessagePackObject]
public class NetworkDataPacket
{
    [Key(0)] public long Timestamp { get; set; }
    [Key(1)] public int ChannelId { get; set; }
    [Key(2)] public double[] Data { get; set; } = Array.Empty<double>();
    [Key(3)] public int ActualLength { get; set; }
    [Key(4)] public Guid PacketId { get; set; } = Guid.NewGuid();
    [Key(5)] public double SampleRate { get; set; }
    [Key(6)] public string SourceNode { get; set; } = string.Empty;
}
