using System.Windows;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private ISettingsService? _settingsService;
    private bool _windowPlacementReady;

    private void RestoreWindowPlacement()
    {
        if (_settingsService is null)
        {
            return;
        }

        try
        {
            var width = _settingsService.Get(SettingKeys.UiSettingsWindowWidth, 0d);
            var height = _settingsService.Get(SettingKeys.UiSettingsWindowHeight, 0d);
            var left = _settingsService.Get(SettingKeys.UiSettingsWindowLeft, double.NaN);
            var top = _settingsService.Get(SettingKeys.UiSettingsWindowTop, double.NaN);
            var stateRaw = _settingsService.Get(SettingKeys.UiSettingsWindowState, "Normal");

            if (width >= MinWidth && height >= MinHeight)
            {
                Width = width;
                Height = height;
            }

            if (!double.IsNaN(left) && !double.IsNaN(top))
            {
                var virtualLeft = SystemParameters.VirtualScreenLeft;
                var virtualTop = SystemParameters.VirtualScreenTop;
                var virtualWidth = SystemParameters.VirtualScreenWidth;
                var virtualHeight = SystemParameters.VirtualScreenHeight;
                if (left + 40 >= virtualLeft &&
                    top + 40 >= virtualTop &&
                    left <= virtualLeft + virtualWidth - 40 &&
                    top <= virtualTop + virtualHeight - 40)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = left;
                    Top = top;
                }
            }

            if (string.Equals(stateRaw, "Maximized", StringComparison.OrdinalIgnoreCase))
            {
                WindowState = WindowState.Maximized;
            }
        }
        catch
        {
            // Placement is best-effort.
        }
    }

    private void SaveWindowPlacement()
    {
        if (_settingsService is null || !_windowPlacementReady)
        {
            return;
        }

        try
        {
            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            if (bounds.Width < MinWidth || bounds.Height < MinHeight)
            {
                return;
            }

            _ = _settingsService.SetAsync(SettingKeys.UiSettingsWindowLeft, bounds.Left, CancellationToken.None);
            _ = _settingsService.SetAsync(SettingKeys.UiSettingsWindowTop, bounds.Top, CancellationToken.None);
            _ = _settingsService.SetAsync(SettingKeys.UiSettingsWindowWidth, bounds.Width, CancellationToken.None);
            _ = _settingsService.SetAsync(SettingKeys.UiSettingsWindowHeight, bounds.Height, CancellationToken.None);
            _ = _settingsService.SetAsync(
                SettingKeys.UiSettingsWindowState,
                WindowState == WindowState.Maximized ? "Maximized" : "Normal",
                CancellationToken.None);
        }
        catch
        {
            // Placement is best-effort.
        }
    }
}
