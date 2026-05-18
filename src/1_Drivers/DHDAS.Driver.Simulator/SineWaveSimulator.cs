using System;
using DHDAS.Contracts.Drivers;
using DHDAS.Contracts.Models;

namespace DHDAS.Driver.Simulator;

public class SineWaveSimulator : IDeviceDriver, IDisposable
{
    public string DeviceName => "Virtual Sine Wave Generator";
    public event Action<int, double[]>? RawDataReceived;

    public const int ChannelCapacity = 2048;

    private const int SampleRate = 1000;
    private const int BatchSize = 100;
    private readonly object _settingsLock = new();
    private readonly Dictionary<int, ChannelInfo> _settings = new();
    private System.Timers.Timer? _timer;
    private double _phase;

    public SineWaveSimulator()
    {
        for (int channelId = 0; channelId < ChannelCapacity; channelId++)
        {
            _settings[channelId] = new ChannelInfo
            {
                ChannelId = channelId,
                IsEnabled = channelId < 8,
                ChannelName = $"CH{channelId + 1:0000}",
                Unit = "V",
                GainDb = 0,
                Offset = 0,
                InputRange = "+/-10V",
                SampleRate = SampleRate
            };
        }
    }

    public void Open()
    {
        _timer = new System.Timers.Timer(100);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    public void ApplyChannelSettings(IReadOnlyList<ChannelInfo> settings)
    {
        lock (_settingsLock)
        {
            foreach (var channel in settings)
            {
                if (channel.ChannelId is >= 0 and < ChannelCapacity)
                {
                    _settings[channel.ChannelId] = channel;
                }
            }
        }
    }

    public ChannelInfo? GetChannelSettings(int channelId)
    {
        lock (_settingsLock)
        {
            return _settings.TryGetValue(channelId, out var channel) ? channel : null;
        }
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        ChannelInfo[] activeChannels;
        lock (_settingsLock)
        {
            activeChannels = _settings.Values
                .Where(channel => channel.IsEnabled)
                .OrderBy(channel => channel.ChannelId)
                .ToArray();
        }

        foreach (var channel in activeChannels)
        {
            double[] buffer = new double[BatchSize];
            double currentPhase = _phase;
            double amplitude = Math.Pow(10, channel.GainDb / 20.0);
            double freq = 2 + channel.ChannelId * 0.2;

            for (int i = 0; i < BatchSize; i++)
            {
                buffer[i] = Math.Sin(2 * Math.PI * freq * currentPhase) * amplitude + channel.Offset;
                currentPhase += 1.0 / Math.Max(channel.SampleRate, 1);
            }

            RawDataReceived?.Invoke(channel.ChannelId, buffer);

            if (channel.ChannelId == activeChannels[^1].ChannelId)
            {
                _phase = currentPhase;
            }
        }

        if (_phase > 1000)
        {
            _phase = 0;
        }
    }

    public void Close()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Close();
}
