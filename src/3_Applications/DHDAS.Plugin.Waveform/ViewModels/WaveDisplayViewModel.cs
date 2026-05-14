using System;
using Avalonia.Threading;
using ReactiveUI;
using System.Reactive.Disposables;
using DHDAS.Application.Support;
using DHDAS.Contracts.Services;
using DHDAS.Contracts.Models;

namespace DHDAS.Plugin.Waveform.ViewModels;

public class WaveformViewModel : PluginViewModelBase
{
    private readonly IWaveformSnapshotService _snapshotService;

    private string _displayData = "等待数据...";
    public string DisplayData
    {
        get => _displayData;
        set => this.RaiseAndSetIfChanged(ref _displayData, value);
    }

    private double[] _samples = Array.Empty<double>();
    public double[] Samples
    {
        get => _samples;
        set => this.RaiseAndSetIfChanged(ref _samples, value);
    }

    public WaveformViewModel(IWaveformSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    public override void OnActivated()
    {
        _snapshotService.Subscribe(snapshot =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Samples = snapshot.Samples;
                    DisplayData = $"通道 {snapshot.ChannelId} | 长度 {snapshot.ActualLength} | 最新接收波形";
                });
            })
            .DisposeWith(Disposables);
    }
}
