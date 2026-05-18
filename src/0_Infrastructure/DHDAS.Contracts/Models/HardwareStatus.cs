namespace DHDAS.Contracts.Models;

public sealed class HardwareStatus
{
    public string DeviceName { get; init; } = string.Empty;
    public bool IsConnected { get; init; }
    public int ChannelCapacity { get; init; }
    public int ActiveChannelCount { get; init; }
    public IReadOnlyList<int> SupportedSampleRates { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> SupportedInputRanges { get; init; } = Array.Empty<string>();
    public DateTimeOffset? LastAppliedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
