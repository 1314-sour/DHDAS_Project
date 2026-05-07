using System;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
// using ReactiveUI.Fody.Helpers;
using System.Reactive.Disposables;
using DHDAS.Application.Support;
using DHDAS.Contracts.Services;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Models;
using Avalonia.ReactiveUI;

namespace DHDAS.Plugin.Waveform.ViewModels;

public class WaveformViewModel : PluginViewModelBase
{
    private readonly IDataPushService _pushService;

    private string _displayData = "等待数据...";
    public string DisplayData
    {
        get => _displayData;
        set => this.RaiseAndSetIfChanged(ref _displayData, value);
    }

    public WaveformViewModel(IDataPushService pushService)
    {
        _pushService = pushService;
    }

    public override void OnActivated()
    {
        Console.WriteLine(">>> 插件：数据订阅通道已开启");

        _pushService.OnRawDataReceived
            .Subscribe(refBuffer =>
            {
                using (refBuffer)
                {
                    if (refBuffer.Data.Data != null && refBuffer.Data.ChannelId == 0)
                    {
                        var val = Math.Round(refBuffer.Data.Data[0], 3);

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            DisplayData = $"通道0 实时值: {val}";
                        });
                    }
                }
            })
            .DisposeWith(Disposables);
    }
}