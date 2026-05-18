using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using Avalonia.Threading;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using ReactiveUI;

namespace DHDAS.Plugin.Waveform.ViewModels;

public class WaveformViewModel : PluginViewModelBase
{
    private readonly IDataPushService _pushService;
    private readonly IMessenger _messenger;
    private readonly IInstrumentService _instrumentService;
    private readonly Dictionary<int, ChannelInfo> _settings = new();
    private readonly Dictionary<int, WaveChannelValueViewModel> _rows = new();
    private readonly Dictionary<int, RawDataSnapshot> _pending = new();
    private readonly object _pendingLock = new();
    private DispatcherTimer? _flushTimer;
    private string _statusText = "等待数据...";

    public WaveformViewModel(
        IDataPushService pushService,
        IMessenger messenger,
        IInstrumentService instrumentService)
    {
        _pushService = pushService;
        _messenger = messenger;
        _instrumentService = instrumentService;

        foreach (var channel in _instrumentService.GetChannelSettings())
        {
            _settings[channel.ChannelId] = channel;
        }
    }

    public ObservableCollection<WaveChannelValueViewModel> Channels { get; } = new();

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public override void OnActivated()
    {
        Console.WriteLine(">>> 插件：多通道数据订阅已开启");

        _pushService.OnRawDataReceived
            .Subscribe(refBuffer =>
            {
                using (refBuffer)
                {
                    var packet = refBuffer.Data;
                    if (packet.Data == null || packet.ActualLength <= 0)
                    {
                        return;
                    }

                    var latest = packet.Data[packet.ActualLength - 1];
                    lock (_pendingLock)
                    {
                        _pending[packet.ChannelId] = new RawDataSnapshot(
                            packet.ChannelId,
                            packet.SampleRate,
                            Math.Round(latest, 4),
                            packet.ActualLength,
                            DateTime.Now.ToString("HH:mm:ss.fff"));
                    }
                }
            })
            .DisposeWith(Disposables);

        _messenger.Listen<ChannelSettingsChangedMessage>()
            .Subscribe(message =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var channel in message.Channels)
                    {
                        _settings[channel.ChannelId] = channel;
                        if (_rows.TryGetValue(channel.ChannelId, out var row))
                        {
                            ApplyChannelMetadata(row, channel);
                        }
                    }
                });
            })
            .DisposeWith(Disposables);

        _flushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _flushTimer.Tick += (_, _) => FlushPendingData();
        _flushTimer.Start();
    }

    public override void OnDeactivated()
    {
        _flushTimer?.Stop();
        _flushTimer = null;
        base.OnDeactivated();
    }

    private void FlushPendingData()
    {
        List<RawDataSnapshot> snapshots;
        lock (_pendingLock)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            snapshots = _pending.Values.OrderBy(snapshot => snapshot.ChannelId).ToList();
            _pending.Clear();
        }

        foreach (var snapshot in snapshots)
        {
            var row = GetOrCreateRow(snapshot.ChannelId);
            row.SampleRate = snapshot.SampleRate;
            row.LatestValue = snapshot.LatestValue;
            row.PacketLength = snapshot.PacketLength;
            row.LastUpdated = snapshot.LastUpdated;
        }

        StatusText = $"已接收 {Channels.Count} 个通道的数据，最近刷新 {DateTime.Now:HH:mm:ss}";
    }

    private WaveChannelValueViewModel GetOrCreateRow(int channelId)
    {
        if (_rows.TryGetValue(channelId, out var row))
        {
            return row;
        }

        row = new WaveChannelValueViewModel(channelId);
        if (_settings.TryGetValue(channelId, out var channel))
        {
            ApplyChannelMetadata(row, channel);
        }
        else
        {
            row.ChannelName = $"CH{channelId + 1:0000}";
            row.Unit = string.Empty;
        }

        _rows[channelId] = row;
        InsertRowSorted(row);
        return row;
    }

    private void InsertRowSorted(WaveChannelValueViewModel row)
    {
        var insertIndex = 0;
        while (insertIndex < Channels.Count && Channels[insertIndex].ChannelId < row.ChannelId)
        {
            insertIndex++;
        }

        Channels.Insert(insertIndex, row);
    }

    private static void ApplyChannelMetadata(WaveChannelValueViewModel row, ChannelInfo channel)
    {
        row.ChannelName = channel.ChannelName;
        row.Unit = channel.Unit;
        row.SampleRate = channel.SampleRate;
    }

    private readonly record struct RawDataSnapshot(
        int ChannelId,
        double SampleRate,
        double LatestValue,
        int PacketLength,
        string LastUpdated);
}
