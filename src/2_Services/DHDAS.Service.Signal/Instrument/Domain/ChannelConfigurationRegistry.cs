using DHDAS.Contracts.Models;

namespace DHDAS.Service.Signal.Instrument.Domain;

public sealed class ChannelConfigurationRegistry
{
    private readonly Dictionary<int, ChannelInfo> _channels = new();

    public ChannelConfigurationRegistry()
    {
        for (int channelId = 0; channelId < ChannelHardwareProfile.ChannelCapacity; channelId++)
        {
            _channels[channelId] = ChannelHardwareProfile.CreateDefaultChannel(channelId);
        }
    }

    public void Apply(IEnumerable<ChannelInfo> settings)
    {
        foreach (var channel in settings)
        {
            _channels[channel.ChannelId] = channel;
        }
    }

    public IReadOnlyList<ChannelInfo> GetAll() =>
        _channels.Values
            .OrderBy(channel => channel.ChannelId)
            .ToArray();

    public int ActiveChannelCount => _channels.Values.Count(channel => channel.IsEnabled);
}
