using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DHDAS.Plugin.Waveform.Controls;

public class WaveformPlotControl : Control
{
    public static readonly StyledProperty<double[]> SamplesProperty =
        AvaloniaProperty.Register<WaveformPlotControl, double[]>(nameof(Samples), Array.Empty<double>());

    public double[] Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    static WaveformPlotControl()
    {
        SamplesProperty.Changed.AddClassHandler<WaveformPlotControl>((control, _) => control.InvalidateVisual());
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        var background = new SolidColorBrush(Color.FromRgb(12, 16, 22));
        context.FillRectangle(background, bounds);

        var plotRect = bounds.Deflate(new Thickness(16));
        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(70, 80, 92)), 1);
        context.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Center.Y), new Point(plotRect.Right, plotRect.Center.Y));
        context.DrawRectangle(null, axisPen, plotRect);

        var samples = Samples;
        if (samples.Length < 2)
        {
            return;
        }

        var min = samples.Min();
        var max = samples.Max();
        if (Math.Abs(max - min) < 0.000001)
        {
            max = min + 1;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < samples.Length; i++)
            {
                var x = plotRect.Left + i * plotRect.Width / (samples.Length - 1);
                var normalized = (samples[i] - min) / (max - min);
                var y = plotRect.Bottom - normalized * plotRect.Height;
                var point = new Point(x, y);

                if (i == 0)
                {
                    ctx.BeginFigure(point, false);
                }
                else
                {
                    ctx.LineTo(point);
                }
            }
        }

        var wavePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 220, 140)), 2);
        context.DrawGeometry(null, wavePen, geometry);
    }
}
