using System.Windows;
using System.Windows.Media;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace GestureClip.App.Controls;

public sealed class GesturePatternView : FrameworkElement
{
    public static readonly DependencyProperty PatternProperty = DependencyProperty.Register(
        nameof(Pattern),
        typeof(string),
        typeof(GesturePatternView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public string Pattern
    {
        get => (string)GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        return new WpfSize(
            double.IsInfinity(availableSize.Width) ? 96 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 64 : availableSize.Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var background = new SolidColorBrush(WpfColor.FromRgb(248, 250, 253));
        var border = new WpfPen(new SolidColorBrush(WpfColor.FromArgb(34, 17, 24, 39)), 1);
        drawingContext.DrawRoundedRectangle(background, border, bounds, 12, 12);

        if (string.Equals(Pattern, "R+L", StringComparison.OrdinalIgnoreCase))
        {
            var text = new FormattedText(
                "右+左",
                System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new System.Windows.Media.FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                14,
                new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(text, new WpfPoint((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2));
            return;
        }

        var points = BuildPoints(Pattern);
        if (points.Count < 2)
        {
            var text = new FormattedText(
                "画线",
                System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                12,
                new SolidColorBrush(WpfColor.FromRgb(124, 133, 150)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(text, new WpfPoint((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2));
            return;
        }

        var normalized = Normalize(points, bounds.Deflate(16));
        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(normalized[0], false, false);
            context.PolyLineTo(normalized.Skip(1).ToArray(), true, true);
        }

        line.Freeze();
        var shadowPen = new WpfPen(new SolidColorBrush(WpfColor.FromArgb(52, 37, 99, 235)), 7)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        var strokePen = new WpfPen(new SolidColorBrush(WpfColor.FromRgb(56, 189, 248)), 4)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        drawingContext.DrawGeometry(null, shadowPen, line);
        drawingContext.DrawGeometry(null, strokePen, line);
        drawingContext.DrawEllipse(WpfBrushes.Black, null, normalized[0], 4.5, 4.5);
        drawingContext.DrawEllipse(new SolidColorBrush(WpfColor.FromRgb(168, 85, 247)), null, normalized[^1], 5, 5);
    }

    private static List<WpfPoint> BuildPoints(string? pattern)
    {
        var points = new List<WpfPoint> { new(0, 0) };
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return points;
        }

        foreach (var direction in pattern.Trim().ToUpperInvariant())
        {
            var last = points[^1];
            points.Add(direction switch
            {
                'U' => new WpfPoint(last.X, last.Y - 1),
                'D' => new WpfPoint(last.X, last.Y + 1),
                'L' => new WpfPoint(last.X - 1, last.Y),
                'R' => new WpfPoint(last.X + 1, last.Y),
                _ => last
            });
        }

        return points;
    }

    private static List<WpfPoint> Normalize(IReadOnlyList<WpfPoint> points, Rect bounds)
    {
        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        var scale = Math.Min(bounds.Width / width, bounds.Height / height);
        if (double.IsInfinity(scale) || double.IsNaN(scale))
        {
            scale = 1;
        }

        var drawingWidth = width * scale;
        var drawingHeight = height * scale;
        var offsetX = bounds.X + (bounds.Width - drawingWidth) / 2;
        var offsetY = bounds.Y + (bounds.Height - drawingHeight) / 2;

        return points
            .Select(point => new WpfPoint(
                offsetX + (point.X - minX) * scale,
                offsetY + (point.Y - minY) * scale))
            .ToList();
    }
}

file static class RectExtensions
{
    public static Rect Deflate(this Rect rect, double amount)
    {
        return new Rect(
            rect.X + amount,
            rect.Y + amount,
            Math.Max(1, rect.Width - amount * 2),
            Math.Max(1, rect.Height - amount * 2));
    }
}

