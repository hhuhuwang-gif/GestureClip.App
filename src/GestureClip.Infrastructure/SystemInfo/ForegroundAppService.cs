using System.Diagnostics;
using System.Text;
using GestureClip.Core.Abstractions;
using GestureClip.Core.SystemInfo;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.SystemInfo;

public sealed class ForegroundAppService : IForegroundAppService
{
    private readonly ILogger<ForegroundAppService> _logger;

    public ForegroundAppService(ILogger<ForegroundAppService> logger)
    {
        _logger = logger;
    }

    public ForegroundAppInfo GetCurrent()
    {
        try
        {
            var hwnd = ForegroundWindowNativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return new ForegroundAppInfo(null, null);
            }

            ForegroundWindowNativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            var process = Process.GetProcessById((int)processId);
            var titleBuffer = new StringBuilder(256);
            ForegroundWindowNativeMethods.GetWindowText(hwnd, titleBuffer, titleBuffer.Capacity);

            return new ForegroundAppInfo($"{process.ProcessName}.exe", RedactWindowTitle(titleBuffer.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read foreground application info.");
            return new ForegroundAppInfo(null, null);
        }
    }

    private static string? RedactWindowTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return title.Length <= 32 ? title : title[..32];
    }
}
