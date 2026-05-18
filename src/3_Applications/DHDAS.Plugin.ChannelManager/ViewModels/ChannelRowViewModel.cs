using DHDAS.Contracts.Models;
using ReactiveUI;

namespace DHDAS.Plugin.ChannelManager.ViewModels;

public sealed class ChannelRowViewModel : ReactiveObject
{
    private bool _isEnabled;
    private string _channelName;
    private string _unit;
    private double _gainDb;
    private double _offset;
    private string _inputRange;
    private int _sampleRate;
    private string _validationMessage = string.Empty;

    public ChannelRowViewModel(int channelId)
    {
        ChannelId = channelId;
        _isEnabled = channelId < 8;
        _channelName = $"CH{channelId + 1:0000}";
        _unit = "V";
        _gainDb = 0;
        _offset = 0;
        _inputRange = "+/-10V";
        _sampleRate = 1000;
    }

    public ChannelRowViewModel(ChannelInfo channel)
    {
        ChannelId = channel.ChannelId;
        _isEnabled = channel.IsEnabled;
        _channelName = channel.ChannelName;
        _unit = channel.Unit;
        _gainDb = channel.GainDb;
        _offset = channel.Offset;
        _inputRange = channel.InputRange;
        _sampleRate = channel.SampleRate;
    }

    public int ChannelId { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

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

    public double GainDb
    {
        get => _gainDb;
        set => this.RaiseAndSetIfChanged(ref _gainDb, value);
    }

    public double Offset
    {
        get => _offset;
        set => this.RaiseAndSetIfChanged(ref _offset, value);
    }

    public string InputRange
    {
        get => _inputRange;
        set => this.RaiseAndSetIfChanged(ref _inputRange, value);
    }

    public int SampleRate
    {
        get => _sampleRate;
        set => this.RaiseAndSetIfChanged(ref _sampleRate, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
    }

    public ChannelInfo ToChannelInfo() => new()
    {
        ChannelId = ChannelId,
        IsEnabled = IsEnabled,
        ChannelName = ChannelName,
        Unit = Unit,
        GainDb = GainDb,
        Offset = Offset,
        InputRange = InputRange,
        SampleRate = SampleRate
    };
}
