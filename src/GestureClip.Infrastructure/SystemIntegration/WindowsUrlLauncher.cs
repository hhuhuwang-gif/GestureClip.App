using System.Diagnostics;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.SystemIntegration;

public sealed class WindowsUrlLauncher : IUrlLauncher
{
    private readonly ILogger<WindowsUrlLauncher> _logger;

    public WindowsUrlLauncher(ILogger<WindowsUrlLauncher> logger)
    {
        _logger = logger;
    }

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open URL.");
        }
    }
}
