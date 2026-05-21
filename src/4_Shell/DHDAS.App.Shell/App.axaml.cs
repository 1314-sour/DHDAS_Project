using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DHDAS.App.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.App.Shell;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var sp = Program.ServiceProvider!;
            var shellVm = sp.GetRequiredService<MainWindowViewModel>();
            shellVm.ReloadPlugins();

            desktop.MainWindow = new MainWindow { DataContext = shellVm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
