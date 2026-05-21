using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface IInstrumentService
{
    InstrumentApplyResult ApplySettings(List<ChannelInfo> settings);
    HardwareStatus GetHardwareStatus();
    IReadOnlyList<ChannelInfo> GetChannelSettings();
}
