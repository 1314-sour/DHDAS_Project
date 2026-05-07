using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using DHDAS.Application.Support;
using DHDAS.Plugin.Waveform.ViewModels;
using Avalonia; // 必须引用这个

namespace DHDAS.Plugin.Waveform;

public class WaveformPlugin : PluginBase
{
    public override string PluginId => "WAVE_001";
    public override string DisplayName => "实时波形显示";

    public override Control CreateView(IServiceProvider sp)
    {
        // 1. 创建 ViewModel (自动注入 IDataPushService)
        var viewModel = ActivatorUtilities.CreateInstance<WaveformViewModel>(sp);

        // 2. 创建一个简单的文本显示界面
        var view = new TextBlock
        {
            FontSize = 24,
            Foreground = Avalonia.Media.Brushes.LimeGreen,
            Margin = new Avalonia.Thickness(20),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        // 3. 绑定数据
        view.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DisplayData"));

        // 4. 设置上下文并激活
        view.DataContext = viewModel;
        viewModel.OnActivated();

        return view;
    }
}