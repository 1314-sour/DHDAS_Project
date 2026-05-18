using DHDAS.Contracts.Models;

namespace DHDAS.Service.Signal.Instrument.Domain;

public sealed class ChannelSettingsPolicy
{
    public IReadOnlyList<string> Validate(IEnumerable<ChannelInfo> settings)
    {
        var errors = new List<string>();

        foreach (var channel in settings)
        {
            if (channel.ChannelId is < 0 or >= ChannelHardwareProfile.ChannelCapacity)
            {
                errors.Add($"CH{channel.ChannelId}: 通道ID超出 0-{ChannelHardwareProfile.ChannelCapacity - 1} 范围");
            }

            if (string.IsNullOrWhiteSpace(channel.ChannelName))
            {
                errors.Add($"CH{channel.ChannelId}: 通道名称不能为空");
            }
            else if (channel.ChannelName.Length > 64)
            {
                errors.Add($"CH{channel.ChannelId}: 通道名称不能超过64个字符");
            }

            if (string.IsNullOrWhiteSpace(channel.Unit))
            {
                errors.Add($"CH{channel.ChannelId}: 物理量单位不能为空");
            }
            else if (channel.Unit.Length > 16)
            {
                errors.Add($"CH{channel.ChannelId}: 物理量单位不能超过16个字符");
            }

            if (double.IsNaN(channel.GainDb) || double.IsInfinity(channel.GainDb) ||
                channel.GainDb < 0 || channel.GainDb > ChannelHardwareProfile.MaxGainDb)
            {
                errors.Add($"CH{channel.ChannelId}: 增益必须在 0-{ChannelHardwareProfile.MaxGainDb} dB 范围内");
            }

            if (double.IsNaN(channel.Offset) || double.IsInfinity(channel.Offset))
            {
                errors.Add($"CH{channel.ChannelId}: 偏移量必须是有效数值");
            }

            if (!ChannelHardwareProfile.SupportedInputRanges.Contains(channel.InputRange))
            {
                errors.Add($"CH{channel.ChannelId}: 输入量程 {channel.InputRange} 不受硬件支持");
            }

            if (!ChannelHardwareProfile.SupportedSampleRates.Contains(channel.SampleRate))
            {
                errors.Add($"CH{channel.ChannelId}: 采样率 {channel.SampleRate}Hz 不受硬件支持");
            }
        }

        return errors;
    }
}
