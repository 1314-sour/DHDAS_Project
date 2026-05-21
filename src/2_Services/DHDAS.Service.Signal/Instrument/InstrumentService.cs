using DHDAS.Contracts.Drivers;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using DHDAS.Service.Signal.Instrument.Domain;
using Microsoft.Extensions.Logging;

namespace DHDAS.Service.Signal.Instrument;

public sealed class InstrumentService : IInstrumentService
{
    private readonly IDeviceDriver _driver;
    private readonly ILogger<InstrumentService> _logger;
    private readonly ChannelSettingsPolicy _policy;
    private readonly ChannelConfigurationRegistry _registry;
    private readonly object _lock = new();
    private DateTimeOffset? _lastAppliedAt;

    public InstrumentService(
        IDeviceDriver driver,
        ChannelSettingsPolicy policy,
        ChannelConfigurationRegistry registry,
        ILogger<InstrumentService> logger)
    {
        _driver = driver;
        _policy = policy;
        _registry = registry;
        _logger = logger;
    }

    public InstrumentApplyResult ApplySettings(List<ChannelInfo> settings)
    {
        if (settings.Count == 0)
        {
            return InstrumentApplyResult.Success(0, "没有需要下发的通道配置");
        }

        var errors = _policy.Validate(settings);
        if (errors.Count > 0)
        {
            _logger.LogInformation("通道配置校验失败，错误数: {ErrorCount}", errors.Count);
            return InstrumentApplyResult.Failure(errors);
        }

        try
        {
            lock (_lock)
            {
                _registry.Apply(settings);
                _driver.ApplyChannelSettings(settings);
                _lastAppliedAt = DateTimeOffset.Now;
            }

            _logger.LogInformation("通道配置已下发到驱动层，通道数: {ChannelCount}", settings.Count);
            return InstrumentApplyResult.Success(settings.Count, $"已下发 {settings.Count} 个通道配置");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "通道配置下发失败");
            return InstrumentApplyResult.Failure(new[] { $"驱动下发失败: {ex.Message}" });
        }
    }

    public HardwareStatus GetHardwareStatus()
    {
        lock (_lock)
        {
            return new HardwareStatus
            {
                DeviceName = _driver.DeviceName,
                IsConnected = true,
                ChannelCapacity = ChannelHardwareProfile.ChannelCapacity,
                ActiveChannelCount = _registry.ActiveChannelCount,
                SupportedSampleRates = ChannelHardwareProfile.SupportedSampleRates,
                SupportedInputRanges = ChannelHardwareProfile.SupportedInputRanges,
                LastAppliedAt = _lastAppliedAt,
                Message = "硬件模拟器在线"
            };
        }
    }

    public IReadOnlyList<ChannelInfo> GetChannelSettings()
    {
        lock (_lock)
        {
            return _registry.GetAll();
        }
    }
}
