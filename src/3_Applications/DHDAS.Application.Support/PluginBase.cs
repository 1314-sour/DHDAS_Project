using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DHDAS.Contracts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Application.Support;

public abstract class PluginBase
{
    public abstract string PluginId { get; }
    public abstract string DisplayName { get; }
    public virtual string? ParentId { get; } = null;
    public virtual int Level { get; } = 1;
    public virtual int Priority { get; } = 0;
    public virtual string Version => "1.0.0";
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public virtual IReadOnlyList<string> PipelineNodeIds => Array.Empty<string>();

    public virtual void RegisterServices(IServiceCollection services)
    {
    }

    public virtual void RegisterServices(IServiceCollection services, PluginRegistrationContext context)
    {
        RegisterServices(services);
    }

    public virtual void Initialize(IServiceProvider serviceProvider)
    {
    }

    public virtual void Mount(IServiceProvider serviceProvider)
    {
    }

    public virtual void Unmount()
    {
    }

    public virtual void Destroy()
    {
    }

    // 核心方法：由插件自己负责创建 View
    public virtual Control? CreateView(IServiceProvider serviceProvider) => null;
}

public sealed class PluginRegistrationContext
{
    public PluginRegistrationContext(
        DistributedRuntimeOptions runtimeOptions,
        IReadOnlyCollection<string> activePipelineNodes)
    {
        RuntimeOptions = runtimeOptions;
        ActivePipelineNodes = activePipelineNodes;
    }

    public DistributedRuntimeOptions RuntimeOptions { get; }
    public IReadOnlyCollection<string> ActivePipelineNodes { get; }
}
