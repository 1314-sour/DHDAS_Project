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
        panel.Children.Add(new TextBlock
        {
            Text = vm.RoleText,
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold
        });

        if (vm.IsSender)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "点击发送后，会发送 1000 点、5 个完整周期的模拟正弦波。接收端请在“实时波形显示”查看完整曲线。",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var btn = new Button { Content = "添加本地回环路由 (CH0 -> 127.0.0.1)" };
            btn.Click += (s, e) => vm.AddRoute();

            var sendBtn = new Button { Content = "发送一次测试正弦数据 (CH0)" };
            sendBtn.Click += (s, e) => vm.SendOnce();

            panel.Children.Add(btn);
            panel.Children.Add(sendBtn);
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = vm.ReceiverText,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
        }

        panel.DataContext = vm;
        return panel;
    }
}
