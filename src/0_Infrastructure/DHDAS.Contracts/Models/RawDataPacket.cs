namespace DHDAS.Contracts.Models;

public struct RawDataPacket
{
    public long Timestamp { get; set; }
    public int ChannelId { get; set; }
    public double SampleRate { get; set; }
    public double[] Data { get; set; }        // 物理数组
    public int ActualLength { get; set; }     // 逻辑有效长度
}