using DHDAS.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Service.Signal.Project;

public static class ProjectModuleExtensions
{
    public static IServiceCollection AddProjectManagerModule(this IServiceCollection services)
    {
        services.AddSingleton<ProjectContext>();
        services.AddSingleton<IProjectContext>(sp => sp.GetRequiredService<ProjectContext>());
        services.AddSingleton<SignalProcessingContext>();
        services.AddSingleton<ISignalProcessingContext>(sp => sp.GetRequiredService<SignalProcessingContext>());
        services.AddSingleton<IProjectService, ProjectService>();
        return services;
    }
}
