using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GestureClip.App.Controls;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using WpfButton = System.Windows.Controls.Button;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfThickness = System.Windows.Thickness;
using WpfVisibility = System.Windows.Visibility;
using WpfFontWeights = System.Windows.FontWeights;
using WpfBrush = System.Windows.Media.Brush;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private void EnableTaskbarMinimizeBehavior()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlStyle);
        SetWindowLong(handle, GwlStyle, style | WsSysmenu | WsMinimizebox | WsMaximizebox | WsThickframe);
    }

    private void EnableBorderlessResize()
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmEnterSizeMove = 0x0231;
        const int wmExitSizeMove = 0x0232;

        if (msg == wmEnterSizeMove)
        {
            SetWindowShadowEnabled(false);
            return IntPtr.Zero;
        }

        if (msg == wmExitSizeMove)
        {
            SetWindowShadowEnabled(true);
            return IntPtr.Zero;
        }

        if (msg != WmNcHitTest || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var screenX = (short)(lParam.ToInt64() & 0xFFFF);
        var screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        System.Windows.Point point;
        try
        {
            point = PointFromScreen(new System.Windows.Point(screenX, screenY));
        }
        catch
        {
            return IntPtr.Zero;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return IntPtr.Zero;
        }

        var border = ResizeBorderThickness;
        var left = point.X >= 0 && point.X <= border;
        var right = point.X >= width - border && point.X <= width + 1;
        var top = point.Y >= 0 && point.Y <= border;
        var bottom = point.Y >= height - border && point.Y <= height + 1;

        var hit = 0;
        if (top && left) hit = HtTopLeft;
        else if (top && right) hit = HtTopRight;
        else if (bottom && left) hit = HtBottomLeft;
        else if (bottom && right) hit = HtBottomRight;
        else if (left) hit = HtLeft;
        else if (right) hit = HtRight;
        else if (top) hit = HtTop;
        else if (bottom) hit = HtBottom;
        else return IntPtr.Zero;

        handled = true;
        return new IntPtr(hit);
    }

    private void SetWindowShadowEnabled(bool enabled)
    {
        Border? chrome = Content as Border;
        if (chrome is null && VisualTreeHelper.GetChildrenCount(this) > 0)
        {
            chrome = VisualTreeHelper.GetChild(this, 0) as Border;
        }

        if (chrome is null)
        {
            return;
        }

        if (enabled)
        {
            chrome.Effect ??= new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.08,
                Direction = 270,
                RenderingBias = RenderingBias.Performance
            };
        }
        else
        {
            chrome.Effect = null;
        }
    }

}
