using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using DHDAS.Application.Support;
using DHDAS.Plugin.ChannelManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Plugin.ChannelManager;

public sealed class ChannelManagerPlugin : PluginBase
{
    private Control? _cachedView;
    private ChannelManagerViewModel? _viewModel;

    public override string PluginId => "CHANNEL_MANAGER";
    public override string DisplayName => "通道管理";
    public override int Priority => 10;

    public override Control CreateView(IServiceProvider serviceProvider)
    {
        if (_cachedView != null)
        {
            return _cachedView;
        }

        _viewModel = ActivatorUtilities.CreateInstance<ChannelManagerViewModel>(serviceProvider);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var toolbar = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var title = new TextBlock
        {
            Text = "2048 通道参数配置",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(title, Dock.Left);
        toolbar.Children.Add(title);

        var refreshButton = new Button
        {
            Content = "刷新状态",
            MinWidth = 88,
            Margin = new Thickness(8, 0, 0, 0)
        };
        refreshButton.Bind(Button.CommandProperty, new Binding(nameof(ChannelManagerViewModel.RefreshStatusCommand)));
        DockPanel.SetDock(refreshButton, Dock.Right);
        toolbar.Children.Add(refreshButton);

        var applyButton = new Button
        {
            Content = "下发全部",
            MinWidth = 88,
            Margin = new Thickness(8, 0, 0, 0)
        };
        applyButton.Bind(Button.CommandProperty, new Binding(nameof(ChannelManagerViewModel.ApplyAllCommand)));
        DockPanel.SetDock(applyButton, Dock.Right);
        toolbar.Children.Add(applyButton);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            IsReadOnly = false,
            RowHeight = 32,
            MinHeight = 320,
            Margin = new Thickness(0, 4, 0, 4)
        };

        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "启用",
            Binding = new Binding(nameof(ChannelRowViewModel.IsEnabled)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(72)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "通道ID",
            Binding = new Binding(nameof(ChannelRowViewModel.ChannelId)),
            IsReadOnly = true,
            Width = new DataGridLength(82)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "通道名称",
            Binding = new Binding(nameof(ChannelRowViewModel.ChannelName)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(150)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "物理量单位",
            Binding = new Binding(nameof(ChannelRowViewModel.Unit)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(100)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "增益(dB)",
            Binding = new Binding(nameof(ChannelRowViewModel.GainDb)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(100)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "偏移量",
            Binding = new Binding(nameof(ChannelRowViewModel.Offset)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(100)
        });
        grid.Columns.Add(CreateComboColumn(
            "输入量程",
            nameof(ChannelRowViewModel.InputRange),
            _viewModel.InputRangeOptions,
            120));
        grid.Columns.Add(CreateComboColumn(
            "采样率",
            nameof(ChannelRowViewModel.SampleRate),
            _viewModel.SampleRateOptions,
            120));
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "校验结果",
            Binding = new Binding(nameof(ChannelRowViewModel.ValidationMessage)),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(ChannelManagerViewModel.Channels)));

        var status = new TextBlock
        {
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(ChannelManagerViewModel.StatusText)));

        Grid.SetRow(toolbar, 0);
        Grid.SetRow(grid, 1);
        Grid.SetRow(status, 2);
        root.Children.Add(toolbar);
        root.Children.Add(grid);
        root.Children.Add(status);
        root.DataContext = _viewModel;
        _viewModel.OnActivated();

        _cachedView = root;
        return root;
    }

    public override void OnUnloaded()
    {
        _viewModel?.OnDeactivated();
        _viewModel = null;
        _cachedView = null;
    }

    private static DataGridTemplateColumn CreateComboColumn(
        string header,
        string bindingPath,
        System.Collections.IEnumerable itemsSource,
        double width)
    {
        return new DataGridTemplateColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            CellTemplate = new FuncDataTemplate<ChannelRowViewModel>((_, _) =>
            {
                var text = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0)
                };
                text.Bind(TextBlock.TextProperty, new Binding(bindingPath));
                return text;
            }),
            CellEditingTemplate = new FuncDataTemplate<ChannelRowViewModel>((_, _) =>
            {
                var combo = new ComboBox
                {
                    ItemsSource = itemsSource,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MinWidth = width - 8
                };
                combo.Bind(ComboBox.SelectedItemProperty, new Binding(bindingPath)
                {
                    Mode = BindingMode.TwoWay
                });
                return combo;
            })
        };
    }
}
