using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using ReactiveUI;

namespace DHDAS.Plugin.ChannelManager.ViewModels;

public sealed class ChannelManagerViewModel : PluginViewModelBase
{
    private readonly IInstrumentService _instrumentService;
    private readonly IMessenger _messenger;
    private readonly List<IDisposable> _rowSubscriptions = new();
    private string _statusText = string.Empty;

    public ChannelManagerViewModel(IInstrumentService instrumentService, IMessenger messenger)
    {
        _instrumentService = instrumentService;
        _messenger = messenger;

        var status = _instrumentService.GetHardwareStatus();
        InputRangeOptions = status.SupportedInputRanges.ToArray();
        SampleRateOptions = status.SupportedSampleRates.ToArray();

        foreach (var channel in _instrumentService.GetChannelSettings())
        {
            var row = new ChannelRowViewModel(channel);
            Channels.Add(row);
            _rowSubscriptions.Add(row.Changed
                .Where(args => args.PropertyName != nameof(ChannelRowViewModel.ValidationMessage))
                .Throttle(TimeSpan.FromMilliseconds(150))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ApplyRow(row)));
        }

        RefreshStatus();
        ApplyAllCommand = ReactiveCommand.Create(ApplyAll);
        RefreshStatusCommand = ReactiveCommand.Create(RefreshStatus);
    }

    public ObservableCollection<ChannelRowViewModel> Channels { get; } = new();
    public IReadOnlyList<string> InputRangeOptions { get; }
    public IReadOnlyList<int> SampleRateOptions { get; }
    public ReactiveCommand<Unit, Unit> ApplyAllCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshStatusCommand { get; }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public override void OnDeactivated()
    {
        foreach (var subscription in _rowSubscriptions)
        {
            subscription.Dispose();
        }

        _rowSubscriptions.Clear();
        base.OnDeactivated();
    }

    private void ApplyRow(ChannelRowViewModel row)
    {
        var channel = row.ToChannelInfo();
        var result = _instrumentService.ApplySettings(new List<ChannelInfo> { channel });
        row.ValidationMessage = result.IsSuccess ? string.Empty : result.Message;
        StatusText = result.Message;

        if (result.IsSuccess)
        {
            _messenger.Send(new ChannelSettingsChangedMessage(
                DateTimeOffset.Now,
                new[] { channel },
                refreshAxisScale: true));
        }
    }

    private void ApplyAll()
    {
        var channels = Channels.Select(row => row.ToChannelInfo()).ToList();
        var result = _instrumentService.ApplySettings(channels);
        StatusText = result.Message;

        if (result.IsSuccess)
        {
            foreach (var row in Channels)
            {
                row.ValidationMessage = string.Empty;
            }

            _messenger.Send(new ChannelSettingsChangedMessage(
                DateTimeOffset.Now,
                channels,
                refreshAxisScale: true));
        }
    }

    private void RefreshStatus()
    {
        var status = _instrumentService.GetHardwareStatus();
        StatusText = $"{status.DeviceName} | 容量 {status.ChannelCapacity} 通道 | 已启用 {status.ActiveChannelCount} 通道 | {status.Message}";
    }
}
