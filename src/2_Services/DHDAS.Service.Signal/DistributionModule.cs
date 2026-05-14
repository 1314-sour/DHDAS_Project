using DHDAS.Contracts.Services;
using DHDAS.Service.Signal.Network;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Service.Signal;

public static class DistributionModule
{
    public static IServiceCollection AddDistributionModule(
        this IServiceCollection services,
        IReadOnlyCollection<string> activePipelineNodes)
    {
        services.AddSingleton<NetworkSenderNode>();
        services.AddSingleton<INetworkService>(sp => sp.GetRequiredService<NetworkSenderNode>());
        services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<NetworkSenderNode>());
        if (activePipelineNodes.Contains(nameof(NetworkSenderNode)))
        {
            services.AddHostedService(sp => sp.GetRequiredService<NetworkSenderNode>());
        }

        services.AddSingleton<NetworkReceiverNode>();
        services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<NetworkReceiverNode>());
        if (activePipelineNodes.Contains(nameof(NetworkReceiverNode)))
        {
            services.AddHostedService(sp => sp.GetRequiredService<NetworkReceiverNode>());
        }

        return services;
    }
}
