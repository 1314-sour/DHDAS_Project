using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using DHDAS.Application.Support;
using DHDAS.Plugin.Waveform.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Plugin.Waveform;

public class WaveformPlugin : PluginBase
{
    private Control? _cachedView;

    public override string PluginId => "WAVE_001";
    public override string DisplayName => "实时波形显示";

    public override Control CreateView(IServiceProvider sp)
    {
        if (_cachedView != null)
        {
            return _cachedView;
        }

        var viewModel = ActivatorUtilities.CreateInstance<WaveformViewModel>(sp);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var title = new TextBlock
        {
            Text = "多通道实时数据监视",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserResizeColumns = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            IsReadOnly = true,
            RowHeight = 30,
            MinHeight = 320
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "通道ID",
            Binding = new Binding(nameof(WaveChannelValueViewModel.ChannelId)),
            Width = new DataGridLength(80)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "通道名称",
            Binding = new Binding(nameof(WaveChannelValueViewModel.ChannelName)),
            Width = new DataGridLength(150)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "单位",
            Binding = new Binding(nameof(WaveChannelValueViewModel.Unit)),
            Width = new DataGridLength(80)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "采样率",
            Binding = new Binding(nameof(WaveChannelValueViewModel.SampleRate)),
            Width = new DataGridLength(100)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "最新值",
            Binding = new Binding(nameof(WaveChannelValueViewModel.LatestValue)),
            Width = new DataGridLength(120)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "包长度",
            Binding = new Binding(nameof(WaveChannelValueViewModel.PacketLength)),
            Width = new DataGridLength(90)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "更新时间",
            Binding = new Binding(nameof(WaveChannelValueViewModel.LastUpdated)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(WaveformViewModel.Channels)));

        var status = new TextBlock
        {
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(WaveformViewModel.StatusText)));

        Grid.SetRow(title, 0);
        Grid.SetRow(grid, 1);
        Grid.SetRow(status, 2);
        root.Children.Add(title);
        root.Children.Add(grid);
        root.Children.Add(status);
        root.DataContext = viewModel;
        viewModel.OnActivated();

        _cachedView = root;
        return root;
    }
}
