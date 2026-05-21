using System;
using Avalonia.Controls;

namespace DHDAS.Application.Support;

public abstract class PluginBase : IDisposable
{
    public abstract string PluginId { get; }
    public abstract string DisplayName { get; }
    public virtual string? ParentId { get; } = null;
    public virtual int Level { get; } = 1;
    public virtual int Priority { get; } = 0;

    public abstract Control CreateView(IServiceProvider serviceProvider);

    public virtual void OnLoaded(IServiceProvider serviceProvider) { }
    public virtual void OnUnloaded() { }
    public virtual void Dispose() => OnUnloaded();
}
