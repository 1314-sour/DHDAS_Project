namespace DHDAS.Contracts.Models;

public sealed class ChannelSettingsChangedMessage
{
    public ChannelSettingsChangedMessage(
        DateTimeOffset timestamp,
        IReadOnlyList<ChannelInfo> channels,
        bool refreshAxisScale)
    {
        Timestamp = timestamp;
        Channels = channels;
        RefreshAxisScale = refreshAxisScale;
    }

    public DateTimeOffset Timestamp { get; }
    public IReadOnlyList<ChannelInfo> Channels { get; }
    public bool RefreshAxisScale { get; }
}
