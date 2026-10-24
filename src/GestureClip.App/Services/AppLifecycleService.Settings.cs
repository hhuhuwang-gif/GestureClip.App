using System.Diagnostics;
using System.Windows;
using GestureClip.App.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.App.Services;

public sealed partial class AppLifecycleService
{
    public void ShowSettingsWindow(string? page = null)
    {
        var startedAt = UiTrace.Restart();
        var createdNow = false;
        if (_settingsWindow is null)
        {
            _settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            createdNow = true;
            UiTrace.Report($"SettingsWindow constructed in {UiTrace.ElapsedMillisecondsSince(startedAt)} ms");
        }

        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
        if (!string.IsNullOrWhiteSpace(page))
        {
            _settingsWindow.NavigateToPage(page);
        }

        // Cold start is what users feel as "slow to open". Reporting construction and the total
        // separately tells us whether the cost sits in XAML inflation or in Show()/layout.
        UiTrace.Report(
            $"Settings window ready in {UiTrace.ElapsedMillisecondsSince(startedAt)} ms ({(createdNow ? "cold" : "warm")})");
        _logger.LogInformation("Settings window shown. Page={Page}", page ?? "default");
    }


    public void ShowGestureBindingEditor(string? pattern = null)
    {
        ShowSettingsWindow("bindings");
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.FocusGestureBinding(pattern);
        _logger.LogInformation("Gesture binding editor shown. Pattern={Pattern}", pattern ?? "(none)");
    }

    public void ShowGestureProfilesWindow(Window owner)
    {
        var window = _serviceProvider.GetRequiredService<GestureProfilesWindow>();
        window.Owner = owner;
        window.ShowDialog();
    }
}
