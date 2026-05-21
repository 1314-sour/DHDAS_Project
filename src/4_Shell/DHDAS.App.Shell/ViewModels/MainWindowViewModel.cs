using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using DHDAS.App.Shell.Services;
using DHDAS.Application.Support;
using ReactiveUI;

namespace DHDAS.App.Shell.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly PluginManager _pluginManager;
    private PluginBase? _selectedPlugin;
    private Control? _currentView;

    public MainWindowViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        ReloadPluginsCommand = ReactiveCommand.Create(ReloadPlugins);
        UnloadSelectedPluginCommand = ReactiveCommand.Create(UnloadSelectedPlugin);

        this.WhenAnyValue(x => x.SelectedPlugin)
            .Where(plugin => plugin != null)
            .Subscribe(plugin =>
            {
                CurrentView = plugin!.CreateView(Program.ServiceProvider!);
            });
    }

    public ObservableCollection<PluginBase> Plugins { get; } = new();

    public ReactiveCommand<Unit, Unit> ReloadPluginsCommand { get; }
    public ReactiveCommand<Unit, Unit> UnloadSelectedPluginCommand { get; }

    public PluginBase? SelectedPlugin
    {
        get => _selectedPlugin;
        set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
    }

    public Control? CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public void ReloadPlugins()
    {
        var previousPluginId = SelectedPlugin?.PluginId;
        Plugins.Clear();

        foreach (var plugin in _pluginManager.LoadPlugins())
        {
            Plugins.Add(plugin);
        }

        SelectedPlugin = Plugins.FirstOrDefault(plugin => plugin.PluginId == previousPluginId)
            ?? Plugins.FirstOrDefault();
    }

    private void UnloadSelectedPlugin()
    {
        var plugin = SelectedPlugin;
        if (plugin == null)
        {
            return;
        }

        DeactivateCurrentView();
        CurrentView = null;
        SelectedPlugin = null;

        if (_pluginManager.UnloadPlugin(plugin))
        {
            Plugins.Remove(plugin);
        }

        SelectedPlugin = Plugins.FirstOrDefault();
    }

    private void DeactivateCurrentView()
    {
        if (CurrentView?.DataContext is PluginViewModelBase viewModel)
        {
            viewModel.OnDeactivated();
        }
    }
}
