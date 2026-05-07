using System;
using System.Timers;
using DHDAS.Contracts.Drivers;

namespace DHDAS.Driver.Simulator;

public class SineWaveSimulator : IDeviceDriver, IDisposable
{
    public string DeviceName => "虚拟正弦波发生器";
    public event Action<int, double[]>? RawDataReceived;

    private System.Timers.Timer? _timer;
    private double _phase = 0;
    private const int ChannelCount = 8;     // 模拟 8 个通道
    private const int SampleRate = 1000;    // 采样率 1000Hz
    private const int BatchSize = 100;      // 每 100ms 发送一次包

    public void Open()
    {
        _timer = new System.Timers.Timer(100);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    // OnTimerElapsed 内部
    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        for (int ch = 0; ch < ChannelCount; ch++)
        {
            double[] buffer = new double[BatchSize];
            // 临时记录本次循环的起始相位，保证 8 个通道相位对齐
            double currentPhase = _phase;

            for (int i = 0; i < BatchSize; i++)
            {
                // 降低频率到 2Hz，采样率 1000 下波形更明显
                double freq = 2;
                buffer[i] = Math.Sin(2 * Math.PI * freq * currentPhase);
                currentPhase += 1.0 / SampleRate;
            }
            RawDataReceived?.Invoke(ch, buffer);

            // 最后一个通道更新完后，正式同步全局相位
            if (ch == ChannelCount - 1) _phase = currentPhase;
        }
        if (_phase > 1000) _phase = 0;
    }

    public void Close()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }

    public void Dispose() => Close();
}