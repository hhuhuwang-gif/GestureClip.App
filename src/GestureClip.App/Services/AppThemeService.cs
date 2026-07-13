using System.Windows;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;

namespace GestureClip.App.Services;

public enum AppUiThemeMode
{
    Light = 0,
    Dark = 1
}

/// <summary>
/// Swaps the Colors resource dictionary so DynamicResource brushes follow light/dark.
/// </summary>
public sealed class AppThemeService
{
    private readonly ISettingsService _settingsService;
    private AppUiThemeMode _mode = AppUiThemeMode.Light;

    public AppThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public AppUiThemeMode Mode => _mode;

    public void InitializeFromSettings()
    {
        var raw = _settingsService.Get(SettingKeys.UiThemeMode, "Light");
        var mode = string.Equals(raw, "Dark", StringComparison.OrdinalIgnoreCase)
            ? AppUiThemeMode.Dark
            : AppUiThemeMode.Light;
        Apply(mode, persist: false);
    }

    public async Task SetModeAsync(AppUiThemeMode mode, CancellationToken cancellationToken = default)
    {
        Apply(mode, persist: false);
        await _settingsService.SetAsync(
            SettingKeys.UiThemeMode,
            mode == AppUiThemeMode.Dark ? "Dark" : "Light",
            cancellationToken);
    }

    public void Apply(AppUiThemeMode mode, bool persist)
    {
        _mode = mode;
        var app = System.Windows.Application.Current;
        if (app?.Resources.MergedDictionaries is null)
        {
            return;
        }

        var source = mode == AppUiThemeMode.Dark
            ? new Uri("Themes/Colors.Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Colors.xaml", UriKind.Relative);

        // Replace first dictionary if it is a Colors* dictionary; else insert at 0.
        ResourceDictionary? existing = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Source is not null &&
                dict.Source.OriginalString.Contains("Colors", StringComparison.OrdinalIgnoreCase))
            {
                existing = dict;
                break;
            }
        }

        var next = new ResourceDictionary { Source = source };
        if (existing is not null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(existing);
            app.Resources.MergedDictionaries.RemoveAt(index);
            app.Resources.MergedDictionaries.Insert(index, next);
        }
        else
        {
            app.Resources.MergedDictionaries.Insert(0, next);
        }

        // Force brush re-bind by reloading Brushes after Colors.
        ResourceDictionary? brushes = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Source is not null &&
                dict.Source.OriginalString.Contains("Brushes", StringComparison.OrdinalIgnoreCase))
            {
                brushes = dict;
                break;
            }
        }

        if (brushes is not null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(brushes);
            app.Resources.MergedDictionaries.RemoveAt(index);
            app.Resources.MergedDictionaries.Insert(
                index,
                new ResourceDictionary { Source = new Uri("Themes/Brushes.xaml", UriKind.Relative) });
        }
    }
}
