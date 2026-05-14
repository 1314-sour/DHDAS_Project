using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using DHDAS.Application.Support;
using DHDAS.Plugin.Waveform.ViewModels;
using Avalonia; // 必须引用这个
using DHDAS.Plugin.Waveform.Controls;

namespace DHDAS.Plugin.Waveform;

public class WaveformPlugin : PluginBase
{
    public override string PluginId => "WAVE_001";
    public override string DisplayName => "实时波形显示";

    public override Control CreateView(IServiceProvider sp)
    {
        // 1. 创建 ViewModel (自动注入 IDataPushService)
        var viewModel = ActivatorUtilities.CreateInstance<WaveformViewModel>(sp);

        var panel = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(20)
        };

        var title = new TextBlock
        {
            FontSize = 18,
            Foreground = Avalonia.Media.Brushes.White
        };
        title.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DisplayData"));

        var plot = new WaveformPlotControl
        {
            Height = 260,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        plot.Bind(WaveformPlotControl.SamplesProperty, new Avalonia.Data.Binding("Samples"));

        panel.Children.Add(title);
        panel.Children.Add(plot);

        // 4. 设置上下文并激活
        panel.DataContext = viewModel;
        viewModel.OnActivated();

        return panel;
    }
}
