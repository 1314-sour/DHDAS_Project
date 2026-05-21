using DHDAS.Contracts.Models;
using DHDAS.Service.Signal.Instrument.Domain;

namespace DHDAS.Service.Signal.Instrument;

public static class ChannelSettingsValidator
{
    public const int ChannelCapacity = ChannelHardwareProfile.ChannelCapacity;
    public const double MaxGainDb = ChannelHardwareProfile.MaxGainDb;

    public static readonly int[] SupportedSampleRates = ChannelHardwareProfile.SupportedSampleRates;

    public static readonly string[] SupportedInputRanges = ChannelHardwareProfile.SupportedInputRanges;

    public static IReadOnlyList<string> Validate(IEnumerable<ChannelInfo> settings) =>
        new ChannelSettingsPolicy().Validate(settings);
}
