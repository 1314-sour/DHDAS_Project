using DHDAS.Contracts.Models;

namespace DHDAS.Service.Signal.Instrument.Domain;

public sealed class ChannelHardwareProfile
{
    public const int ChannelCapacity = 2048;
    public const double MaxGainDb = 100;

    public static readonly int[] SupportedSampleRates =
    {
        1000,
        2000,
        5000,
        10000,
        20000,
        50000,
        100000
    };

    public static readonly string[] SupportedInputRanges =
    {
        "+/-1V",
        "+/-5V",
        "+/-10V",
        "4-20mA"
    };

    public static ChannelInfo CreateDefaultChannel(int channelId) => new()
    {
        ChannelId = channelId,
        IsEnabled = channelId < 8,
        ChannelName = $"CH{channelId + 1:0000}",
        Unit = "V",
        GainDb = 0,
        Offset = 0,
        InputRange = "+/-10V",
        SampleRate = 1000
    };
}
