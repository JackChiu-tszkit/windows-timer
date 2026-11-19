using System.Windows;
using System.Windows.Media;

namespace WindowsTimer.Controls;

public class RingProgress : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness),
        typeof(double),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IndicatorBrushProperty = DependencyProperty.Register(
        nameof(IndicatorBrush),
        typeof(Brush),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush? IndicatorBrush
    {
        get => (Brush?)GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var stroke = Math.Max(1, Thickness);
        var size = Math.Min(width, height);
        var radius = (size - stroke) / 2;
        if (radius <= 0)
            return;

        var center = new Point(width / 2, height / 2);
        var track = CreatePen(TrackBrush ?? Brushes.DimGray, stroke);
        var indicator = CreatePen(IndicatorBrush ?? Brushes.CornflowerBlue, stroke);
        drawingContext.DrawEllipse(null, track, center, radius, radius);

        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0.0005)
            return;

        if (progress >= 0.9995)
        {
            drawingContext.DrawEllipse(null, indicator, center, radius, radius);
            return;
        }

        drawingContext.DrawGeometry(null, indicator, CreateArc(center, radius, progress));
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        return new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }

    private static StreamGeometry CreateArc(Point center, double radius, double progress)
    {
        var startAngle = -Math.PI / 2;
        var sweep = progress * Math.PI * 2;
        var endAngle = startAngle + sweep;
        var start = new Point(center.X + (radius * Math.Cos(startAngle)), center.Y + (radius * Math.Sin(startAngle)));
        var end = new Point(center.X + (radius * Math.Cos(endAngle)), center.Y + (radius * Math.Sin(endAngle)));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.ArcTo(
                end,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: progress > 0.5,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }
}
