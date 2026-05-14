using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DHDAS.Plugin.NetworkConfig.Controls;

public class SinePreviewControl : Control
{
    public static readonly StyledProperty<double[]> SamplesProperty =
        AvaloniaProperty.Register<SinePreviewControl, double[]>(nameof(Samples), Array.Empty<double>());

    public double[] Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    static SinePreviewControl()
    {
        SamplesProperty.Changed.AddClassHandler<SinePreviewControl>((control, _) => control.InvalidateVisual());
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = Bounds.Deflate(new Thickness(12));
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(12, 16, 22)), Bounds);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(70, 80, 92)), 1), rect);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(70, 80, 92)), 1), new Point(rect.Left, rect.Center.Y), new Point(rect.Right, rect.Center.Y));

        var samples = Samples;
        if (samples.Length < 2) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < samples.Length; i++)
            {
                var x = rect.Left + i * rect.Width / (samples.Length - 1);
                var y = rect.Center.Y - samples[i] * rect.Height * 0.45;
                var point = new Point(x, y);
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(80, 180, 255)), 2), geometry);
    }
}
