using System;
using Avalonia;
using Avalonia.Controls; // 解决 Control 找不到的问题
using Avalonia.Layout;
using Microsoft.Extensions.DependencyInjection;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
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
                Text = "计算节点映射表：配置目标节点、IP、端口和负责转发的通道范围。选中路由表中的目标节点，生成对应通道正弦波并发送。",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var inputGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*,80,80,80"),
                RowDefinitions = new RowDefinitions("Auto,Auto")
            };

            AddCell(inputGrid, "节点名称", 0, 0);
            AddCell(inputGrid, "IP地址", 0, 1);
            AddCell(inputGrid, "端口", 0, 2);
            AddCell(inputGrid, "起始通道", 0, 3);
            AddCell(inputGrid, "结束通道", 0, 4);

            var nodeName = AddTextBox(inputGrid, vm.InputNodeName, 1, 0);
            var ip = AddTextBox(inputGrid, vm.InputIp, 1, 1);
            var port = AddTextBox(inputGrid, vm.InputPort.ToString(), 1, 2);
            var startChannel = AddTextBox(inputGrid, vm.StartChannelId.ToString(), 1, 3);
            var endChannel = AddTextBox(inputGrid, vm.EndChannelId.ToString(), 1, 4);

            panel.Children.Add(inputGrid);

            var addRouteButton = new Button { Content = "添加/应用路由" };
            addRouteButton.Click += (s, e) =>
            {
                vm.InputNodeName = nodeName.Text ?? "本地回环节点";
                vm.InputIp = ip.Text ?? "127.0.0.1";
                vm.InputPort = int.TryParse(port.Text, out var parsedPort) ? parsedPort : 5000;
                vm.StartChannelId = int.TryParse(startChannel.Text, out var parsedStart) ? parsedStart : 0;
                vm.EndChannelId = int.TryParse(endChannel.Text, out var parsedEnd) ? parsedEnd : vm.StartChannelId;
                vm.AddRoute();
            };

            panel.Children.Add(addRouteButton);

            var testChannelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            testChannelPanel.Children.Add(new TextBlock
            {
                Text = "测试通道",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });
            var testChannel = new TextBox
            {
                Text = vm.TestChannelId.ToString(),
                Width = 80
            };
            testChannelPanel.Children.Add(testChannel);

            var generateBtn = new Button { Content = "生成指定通道正弦波" };
            generateBtn.Click += (s, e) =>
            {
                vm.TestChannelId = int.TryParse(testChannel.Text, out var parsedTestChannel) ? parsedTestChannel : 0;
                vm.GenerateWaveform();
            };

            var sendBtn = new Button { Content = "发送到选中路由" };
            sendBtn.Click += (s, e) => vm.SendOnce();

            var waveformButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { generateBtn, sendBtn }
            };
            panel.Children.Add(testChannelPanel);
            panel.Children.Add(waveformButtons);

            panel.Children.Add(new TextBlock { Text = "当前路由表", FontWeight = Avalonia.Media.FontWeight.Bold });
            var routeList = new ListBox
            {
                ItemsSource = vm.Routes,
                MinHeight = 80,
                MaxHeight = 120
            };
            routeList.SelectionChanged += (s, e) =>
            {
                if (routeList.SelectedItem is not NetworkRoute route) return;
                vm.SelectedRoute = route;
                vm.TestChannelId = route.StartChannelId;
                testChannel.Text = route.StartChannelId.ToString();
            };
            panel.Children.Add(routeList);

            var connectButton = new Button { Content = "连接选中路由" };
            connectButton.Click += (s, e) => vm.ConnectSelected();

            var disconnectButton = new Button { Content = "断开选中路由" };
            disconnectButton.Click += (s, e) => vm.DisconnectSelected();

            var linkButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { connectButton, disconnectButton }
            };
            panel.Children.Add(linkButtons);

            panel.Children.Add(new TextBlock { Text = "网络链路状态（实时流量 / 丢包统计）", FontWeight = Avalonia.Media.FontWeight.Bold });
            panel.Children.Add(new ListBox
            {
                ItemsSource = vm.LinkStatuses,
                MinHeight = 80,
                MaxHeight = 140
            });
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
        vm.OnActivated();
        return new ScrollViewer { Content = panel };
    }

    private static void AddCell(Grid grid, string text, int row, int column)
    {
        var block = new TextBlock { Text = text, FontWeight = Avalonia.Media.FontWeight.Bold };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private static TextBox AddTextBox(Grid grid, string text, int row, int column)
    {
        var textBox = new TextBox { Text = text };
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, column);
        grid.Children.Add(textBox);
        return textBox;
    }
}
