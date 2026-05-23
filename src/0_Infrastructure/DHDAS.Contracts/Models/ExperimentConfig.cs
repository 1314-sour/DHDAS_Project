namespace DHDAS.Contracts.Models;

public class ExperimentConfig
{
    public string Notes { get; set; } = string.Empty;
    public List<ChannelInfo> ChannelSettings { get; set; } = new();
}
