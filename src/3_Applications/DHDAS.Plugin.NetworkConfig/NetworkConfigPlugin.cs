using System;
using Avalonia;
using Avalonia.Controls; // 解决 Control 找不到的问题
using Avalonia.Layout;
using Microsoft.Extensions.DependencyInjection;
using DHDAS.Application.Support;
using DHDAS.Plugin.NetworkConfig.Controls;
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
                Text = "待发送的模拟正弦波数据预览",
                Foreground = Avalonia.Media.Brushes.White
            });

            var preview = new SinePreviewControl
            {
                Height = 180,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };
            preview.Bind(SinePreviewControl.SamplesProperty, new Avalonia.Data.Binding("TestSamples"));
            panel.Children.Add(preview);

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
