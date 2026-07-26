using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Win32;

namespace GestureClip.Infrastructure.SystemInfo;

/// <summary>
/// System-wide input idle time via GetLastInputInfo. Tick arithmetic is done in
/// uint so the 49.7-day wrap of the tick counter cancels out.
/// </summary>
public sealed class WindowsUserIdleProvider : IUserIdleProvider
{
    public TimeSpan GetIdleDuration()
    {
        var info = new IdleNativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<IdleNativeMethods.LASTINPUTINFO>()
        };

        if (!IdleNativeMethods.GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var elapsedMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsedMs);
    }
}
