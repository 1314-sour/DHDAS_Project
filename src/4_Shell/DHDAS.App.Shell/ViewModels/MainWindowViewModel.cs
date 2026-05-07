using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using DHDAS.Application.Support;

namespace DHDAS.App.Shell.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    // 自动发现的所有插件列表
    public ObservableCollection<PluginBase> Plugins { get; } = new();

    // 当前选中的插件
    [Reactive] public PluginBase? SelectedPlugin { get; set; }

    // 右侧展示的视图实例
    [Reactive] public Control? CurrentView { get; set; }

    public MainWindowViewModel()
    {
        // 监控选中项，实现自动切换
        this.WhenAnyValue(x => x.SelectedPlugin)
            .Where(p => p != null)
            .Subscribe(p =>
            {
                // 注意：这里调用 CreateView 传入主程序的 ServiceProvider
                CurrentView = p!.CreateView(Program.ServiceProvider!);
            });
    }
}