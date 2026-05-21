using System;
using Avalonia;
using Avalonia.Controls; // 解决 Control 找不到的问题
using Avalonia.Layout;
using Microsoft.Extensions.DependencyInjection;
using DHDAS.Application.Support;
using DHDAS.Plugin.NetworkConfig.ViewModels;

namespace DHDAS.Plugin.NetworkConfig;

public class NetworkConfigPlugin : PluginBase
{
    public override string PluginId => "NETWORK_CONFIG";
    public override string DisplayName => "分布式路由配置";
    public override int Level => 2;
    public override string ParentId => "SYSTEM_SETTINGS";

    public override Control CreateView(IServiceProvider sp)
    {
        var vm = ActivatorUtilities.CreateInstance<NetworkConfigViewModel>(sp);

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        var btn = new Button { Content = "添加本地回环路由 (CH0 -> 127.0.0.1)" };
        btn.Click += (s, e) => vm.AddRoute();

        panel.Children.Add(btn);
        panel.DataContext = vm;
        return panel;
    }
}