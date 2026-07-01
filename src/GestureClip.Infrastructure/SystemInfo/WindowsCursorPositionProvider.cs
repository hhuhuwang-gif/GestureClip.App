using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Infrastructure.SystemInfo;

public sealed class WindowsCursorPositionProvider : ICursorPositionProvider
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public CursorPosition GetCurrentPosition()
    {
        if (!GetCursorPos(out var point))
        {
            return new CursorPosition(0, 0, DateTimeOffset.UtcNow);
        }

        return new CursorPosition(point.X, point.Y, DateTimeOffset.UtcNow);
    }

    public ScreenBounds GetVirtualScreenBounds()
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        return new ScreenBounds(left, top, left + width, top + height);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
