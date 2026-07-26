using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace GestureClip.App.Services;

/// <summary>
/// Applies the Win11 Mica system backdrop to a window. Callers must use a non-layered
/// window (AllowsTransparency=False) — DWM backdrops never composite on layered windows.
/// </summary>
public static class WindowBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int CornerPreferenceRound = 2;
    private const int BackdropTypeMica = 2;

    /// <summary>DWMWA_SYSTEMBACKDROP_TYPE requires Win11 22H2.</summary>
    public static bool IsMicaSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    public static bool TryApplyMica(Window window, bool isDark)
    {
        if (!IsMicaSupported)
        {
            return false;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        // Let the backdrop show through the WPF surface.
        if (HwndSource.FromHwnd(handle) is { CompositionTarget: not null } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        SetIntAttribute(handle, DwmwaUseImmersiveDarkMode, isDark ? 1 : 0);
        SetIntAttribute(handle, DwmwaWindowCornerPreference, CornerPreferenceRound);
        return SetIntAttribute(handle, DwmwaSystemBackdropType, BackdropTypeMica);
    }

    public static void UpdateDarkMode(Window window, bool isDark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            SetIntAttribute(handle, DwmwaUseImmersiveDarkMode, isDark ? 1 : 0);
        }
    }

    private static bool SetIntAttribute(IntPtr handle, int attribute, int value)
    {
        return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
