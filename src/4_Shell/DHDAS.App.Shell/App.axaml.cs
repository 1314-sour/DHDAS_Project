using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DHDAS.App.Shell.ViewModels;
using DHDAS.App.Shell.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace DHDAS.App.Shell;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var sp = Program.ServiceProvider!;
            var pm = sp.GetRequiredService<DHDAS.App.Shell.Services.PluginManager>();

            // 1. 执行动态加载
            pm.LoadPlugins();

            // 2. 获取 ViewModel
            var shellVm = sp.GetRequiredService<MainWindowViewModel>();

            // 3. 将加载到的插件填入 ViewModel 供 UI 渲染
            foreach (var p in pm.GetPlugins())
            {
                shellVm.Plugins.Add(p);
            }

            // 4. 默认选中第一个
            shellVm.SelectedPlugin = shellVm.Plugins.FirstOrDefault();

            desktop.MainWindow = new MainWindow { DataContext = shellVm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}