using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GestureClip.App;

/// <summary>
/// Snipaste-style pinned image: a topmost floating window showing one image from
/// clipboard history. Drag to move, wheel to zoom, double-click / Esc to close.
/// </summary>
public partial class PinImageWindow : Window
{
    private const double MinScale = 0.1;
    private const double MaxScale = 5.0;
    private double _scale = 1.0;
    private readonly double _baseWidth;
    private readonly double _baseHeight;

    public PinImageWindow(BitmapSource image)
    {
        InitializeComponent();
        PinnedImage.Source = image;

        // Start at 1:1 pixels, clamped to 70% of the work area.
        var workArea = SystemParameters.WorkArea;
        _baseWidth = image.PixelWidth;
        _baseHeight = image.PixelHeight;
        var fit = Math.Min(1.0, Math.Min(
            workArea.Width * 0.7 / Math.Max(1, _baseWidth),
            workArea.Height * 0.7 / Math.Max(1, _baseHeight)));
        _scale = fit;
        ApplyScale();

        Left = workArea.Left + (workArea.Width - _baseWidth * _scale) / 2;
        Top = workArea.Top + (workArea.Height - _baseHeight * _scale) / 2;
    }

    private void ApplyScale()
    {
        PinnedImage.Width = Math.Max(24, _baseWidth * _scale);
        PinnedImage.Height = Math.Max(24, _baseHeight * _scale);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        _scale = Math.Clamp(_scale * factor, MinScale, MaxScale);
        ApplyScale();
        e.Handled = true;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
