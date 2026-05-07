using System;
using Avalonia.Controls;

namespace DHDAS.Application.Support;

public abstract class PluginBase
{
    public abstract string PluginId { get; }
    public abstract string DisplayName { get; }
    public virtual string? ParentId { get; } = null;
    public virtual int Level { get; } = 1;
    public virtual int Priority { get; } = 0;

    // 核心方法：由插件自己负责创建 View 
    public abstract Control CreateView(IServiceProvider serviceProvider);
}