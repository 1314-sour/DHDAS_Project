namespace DHDAS.Contracts.Models;

public class WaveformSnapshot
{
    public int ChannelId { get; set; }
    public long Timestamp { get; set; }
    public double[] Samples { get; set; } = Array.Empty<double>();
    public int ActualLength { get; set; }
}
