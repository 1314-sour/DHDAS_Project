using ReactiveUI;

namespace DHDAS.Plugin.Waveform.ViewModels;

public sealed class WaveChannelValueViewModel : ReactiveObject
{
    private string _channelName = string.Empty;
    private string _unit = string.Empty;
    private double _sampleRate;
    private double _latestValue;
    private int _packetLength;
    private string _lastUpdated = string.Empty;

    public WaveChannelValueViewModel(int channelId)
    {
        ChannelId = channelId;
    }

    public int ChannelId { get; }

    public string ChannelName
    {
        get => _channelName;
        set => this.RaiseAndSetIfChanged(ref _channelName, value);
    }

    public string Unit
    {
        get => _unit;
        set => this.RaiseAndSetIfChanged(ref _unit, value);
    }

    public double SampleRate
    {
        get => _sampleRate;
        set => this.RaiseAndSetIfChanged(ref _sampleRate, value);
    }

    public double LatestValue
    {
        get => _latestValue;
        set => this.RaiseAndSetIfChanged(ref _latestValue, value);
    }

    public int PacketLength
    {
        get => _packetLength;
        set => this.RaiseAndSetIfChanged(ref _packetLength, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        set => this.RaiseAndSetIfChanged(ref _lastUpdated, value);
    }
}
