using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;

namespace DHDAS.App.Shell.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly IDisposable _feedbackSubscription;

    // 自动发现的所有插件列表
    public ObservableCollection<PluginBase> Plugins { get; } = new();

    // 当前选中的插件
    [Reactive] public PluginBase? SelectedPlugin { get; set; }

    // 右侧展示的视图实例
    [Reactive] public Control? CurrentView { get; set; }

    public MainWindowViewModel(IDistributedFeedbackService feedbackService)
    {
        _feedbackSubscription = feedbackService.Subscribe(ShowFeedback);

        // 监控选中项，实现自动切换
        this.WhenAnyValue(x => x.SelectedPlugin)
            .Where(p => p != null)
            .Subscribe(p =>
            {
                // 注意：这里调用 CreateView 传入主程序的 ServiceProvider
                CurrentView = p!.CreateView(Program.ServiceProvider!);
            });
    }

    private static void ShowFeedback(DistributedFeedbackMessage feedback)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var dialog = BuildFeedbackWindow(feedback);
            var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (owner != null)
            {
                dialog.Show(owner);
            }
            else
            {
                dialog.Show();
            }

            _ = AutoCloseAsync(dialog);
        });
    }

    private static Window BuildFeedbackWindow(DistributedFeedbackMessage feedback)
    {
        var title = string.IsNullOrWhiteSpace(feedback.Title) ? "分布式反馈" : feedback.Title;

        var message = new TextBlock
        {
            Text = feedback.Message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var time = new TextBlock
        {
            Text = feedback.Timestamp.ToString("HH:mm:ss"),
            Opacity = 0.7,
            FontSize = 12
        };

        var okButton = new Button
        {
            Content = "确定",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 72
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = $"{title} [{feedback.Level}]",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    FontSize = 16
                },
                message,
                time,
                okButton
            }
        };

        var window = new Window
        {
            Title = "分布式链路反馈",
            Width = 380,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => window.Close();
        return window;
    }

    private static async Task AutoCloseAsync(Window window)
    {
        await Task.Delay(3500);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        });
    }
}
