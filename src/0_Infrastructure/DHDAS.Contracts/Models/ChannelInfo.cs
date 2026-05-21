namespace DHDAS.Contracts.Models;

public struct ChannelInfo
{
    public int ChannelId { get; set; }
    public bool IsEnabled { get; set; }
    public string ChannelName { get; set; }
    public string Unit { get; set; }
    public double GainDb { get; set; }
    public double Offset { get; set; }
    public string InputRange { get; set; }
    public int SampleRate { get; set; }
}
