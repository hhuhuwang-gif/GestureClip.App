using System.Windows;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using Microsoft.Win32;

namespace GestureClip.App.Services;

public enum AppUiThemeMode
{
    Light = 0,
    Dark = 1
}

public enum AppThemePreference
{
    Light = 0,
    Dark = 1,
    System = 2
}

/// <summary>
/// Swaps the Colors resource dictionary so DynamicResource brushes follow light/dark,
/// resolves the "follow system" preference, and layers an accent-color override dictionary.
/// </summary>
public sealed partial class AppThemeService : IDisposable
{
    private const string AccentOverlayMarkerKey = "GestureClipAccentOverlay";

    private readonly ISettingsService _settingsService;
    private AppUiThemeMode _mode = AppUiThemeMode.Light;
    private AppThemePreference _preference = AppThemePreference.Light;
    private string _accentColorHex = string.Empty;
    private bool _systemWatcherHooked;

    public AppThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>Raised after a theme (mode or accent) has been applied to application resources.</summary>
    public event EventHandler? Changed;

    public AppUiThemeMode Mode => _mode;

    public AppThemePreference Preference => _preference;

    public string AccentColorHex => _accentColorHex;

    public void InitializeFromSettings()
    {
        _preference = ParsePreference(_settingsService.Get(SettingKeys.UiThemeMode, "Light"));
        _accentColorHex = _settingsService.Get(SettingKeys.UiAccentColor, string.Empty) ?? string.Empty;
        HookSystemWatcher();
        Apply(ResolveMode(_preference));
    }

    public static AppThemePreference ParsePreference(string? raw)
    {
        var value = raw?.Trim();
        if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return AppThemePreference.Dark;
        }

        if (string.Equals(value, "System", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return AppThemePreference.System;
        }

        return AppThemePreference.Light;
    }

    public static string PreferenceToSettingValue(AppThemePreference preference) => preference switch
    {
        AppThemePreference.Dark => "Dark",
        AppThemePreference.System => "System",
        _ => "Light"
    };

    public async Task SetPreferenceAsync(AppThemePreference preference, CancellationToken cancellationToken = default)
    {
        _preference = preference;
        HookSystemWatcher();
        Apply(ResolveMode(preference));
        await _settingsService.SetAsync(
            SettingKeys.UiThemeMode,
            PreferenceToSettingValue(preference),
            cancellationToken);
    }

    public async Task SetAccentColorAsync(string accentColorHex, CancellationToken cancellationToken = default)
    {
        _accentColorHex = accentColorHex?.Trim() ?? string.Empty;
        Apply(_mode);
        await _settingsService.SetAsync(SettingKeys.UiAccentColor, _accentColorHex, cancellationToken);
    }

    public static bool IsSystemInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Apply(AppUiThemeMode mode)
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

        ApplyAccentOverlay(app, mode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_systemWatcherHooked)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _systemWatcherHooked = false;
        }
    }

    private static AppUiThemeMode ResolveMode(AppThemePreference preference) => preference switch
    {
        AppThemePreference.Dark => AppUiThemeMode.Dark,
        AppThemePreference.System => IsSystemInDarkMode() ? AppUiThemeMode.Dark : AppUiThemeMode.Light,
        _ => AppUiThemeMode.Light
    };

    private void HookSystemWatcher()
    {
        if (_systemWatcherHooked)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _systemWatcherHooked = true;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Windows raises General (not a dedicated category) when apps light/dark flips.
        if (_preference != AppThemePreference.System || e.Category != UserPreferenceCategory.General)
        {
            return;
        }

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            () => Apply(ResolveMode(AppThemePreference.System)));
    }

    private void ApplyAccentOverlay(System.Windows.Application app, AppUiThemeMode mode)
    {
        ResourceDictionary? overlay = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Contains(AccentOverlayMarkerKey))
            {
                overlay = dict;
                break;
            }
        }

        if (overlay is not null)
        {
            app.Resources.MergedDictionaries.Remove(overlay);
        }

        if (!AccentPalette.TryParse(_accentColorHex, out var accent))
        {
            return;
        }

        // Appended last so it wins every DynamicResource lookup over Colors/Brushes.
        app.Resources.MergedDictionaries.Add(BuildAccentOverlay(accent, mode == AppUiThemeMode.Dark));
    }

}
