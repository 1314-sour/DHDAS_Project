using DHDAS.Contracts.Services;
using DHDAS.Service.Signal.Instrument.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Service.Signal.Instrument;

public static class InstrumentModuleExtensions
{
    public static IServiceCollection AddChannelManagerModule(this IServiceCollection services) =>
        services.AddInstrumentModule();

    public static IServiceCollection AddInstrumentModule(this IServiceCollection services)
    {
        services.AddSingleton<ChannelSettingsPolicy>();
        services.AddSingleton<ChannelConfigurationRegistry>();
        services.AddSingleton<InstrumentService>();
        services.AddSingleton<IInstrumentService>(sp => sp.GetRequiredService<InstrumentService>());
        return services;
    }
}
