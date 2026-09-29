using System.Windows;
using System.Windows.Media;

namespace GpuReduct.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Faint guides at 25 / 50 / 75 %
        var guide = new Pen(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)), 1);
        guide.Freeze();
        for (int i = 1; i <= 3; i++)
        {
            double y = Math.Round(h * i / 4) + 0.5;
            dc.DrawLine(guide, new Point(0, y), new Point(w, y));
        }

        var values = Values;
        if (values is null || values.Count < 2) return;

        double max = Maximum > 0 ? Maximum : 1;
        double step = w / (values.Count - 1);
        Point At(int i) => new(i * step, h - Math.Clamp(values[i] / max, 0, 1) * (h - 2) - 1);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(At(0), false, false);
            for (int i = 1; i < values.Count; i++) ctx.LineTo(At(i), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(0, h), true, true);
            for (int i = 0; i < values.Count; i++) ctx.LineTo(At(i), true, false);
            ctx.LineTo(new Point(w, h), true, false);
        }
        area.Freeze();

        var c = (Stroke as SolidColorBrush)?.Color ?? Colors.White;
        var fill = new LinearGradientBrush(Color.FromArgb(0x40, c.R, c.G, c.B), Color.FromArgb(0x00, c.R, c.G, c.B), 90);
        fill.Freeze();

        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round }, line);
        dc.DrawEllipse(Stroke, null, At(values.Count - 1), 3, 3); // "now" dot
    }
}
