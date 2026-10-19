using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{
    [Fact]
    public void App_merges_glass_theme_resource_dictionaries()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml");
        var appXaml = File.ReadAllText(appPath);

        Assert.Contains("Themes/Colors.xaml", appXaml);
        Assert.Contains("Themes/Brushes.xaml", appXaml);
        Assert.Contains("Themes/Controls.xaml", appXaml);
        Assert.Contains("Themes/GlassStyles.xaml", appXaml);
    }

    [Fact]
    public void App_supports_smoke_exit_after_startup_for_release_verification()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs");
        var source = File.ReadAllText(appPath);

        Assert.Contains("--smoke-exit-after-startup", source);
        Assert.Contains("ExitApplication()", source);
    }

    [Fact]
    public void Clipboard_writer_uses_native_unicode_text_path_for_large_text_copy()
    {
        var writerPath = FindRepositoryFile("src", "GestureClip.Infrastructure", "Clipboard", "WpfClipboardWriter.cs");
        var nativePath = FindRepositoryFile("src", "GestureClip.Infrastructure", "Win32", "ClipboardNativeMethods.cs");
        var writer = File.ReadAllText(writerPath);
        var native = File.ReadAllText(nativePath);

        Assert.Contains("SetUnicodeTextNative", writer);
        Assert.DoesNotContain("Clipboard.SetText(text)", writer);
        Assert.Contains("CF_UNICODETEXT", native);
        Assert.Contains("SetClipboardData", native);
        Assert.Contains("GlobalAlloc", native);
    }

    [Fact]
    public void App_exposes_one_click_update_entry_to_latest_release()
    {
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var settingsXamlPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var lifecycle = File.ReadAllText(lifecyclePath);
        var tray = File.ReadAllText(trayPath);
        var settingsXaml = File.ReadAllText(settingsXamlPath);
        var settingsSource = ReadAllSettingsWindowSources();

        Assert.Contains("CheckForUpdatesAsync", lifecycle);
        Assert.Contains("StartCoverUpdateAsync", lifecycle);
        Assert.Contains("IUpdateCheckService", lifecycle);
        Assert.Contains("IUpdateInstallerService", lifecycle);
        Assert.Contains("检查更新", tray);
        Assert.Contains("一键覆盖更新", tray);
        Assert.Contains("检查更新", settingsXaml);
        Assert.Contains("一键覆盖更新", settingsXaml);
        Assert.Contains("GitHub Latest Release", settingsXaml);
        Assert.Contains("CheckUpdateButton_Click", settingsXaml);
        Assert.Contains("UpdateButton_Click", settingsXaml);
        Assert.Contains("CheckForUpdatesAsync", settingsSource);
        Assert.Contains("StartCoverUpdateAsync", settingsSource);
    }
    [Fact]
    public void App_icon_click_and_second_launch_toggle_settings_window()
    {
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs");
        var singlePath = FindRepositoryFile("src", "GestureClip.App", "Services", "SingleInstanceService.cs");

        var tray = File.ReadAllText(trayPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var app = File.ReadAllText(appPath);
        var single = File.ReadAllText(singlePath);

        Assert.Contains("ToggleSettingsWindow", lifecycle);
        Assert.Contains("_settingsWindow.Hide()", lifecycle);
        Assert.Contains("ToggleSettingsWindow", tray);
        Assert.Contains("MouseClick", tray);
        Assert.Contains("ActivationRequested", app);
        Assert.Contains("SignalExistingInstance", single);
        var settingsWindowSource = ReadAllSettingsWindowSources();
        Assert.Contains("EnableTaskbarMinimizeBehavior", settingsWindowSource);
        Assert.Contains("WsMinimizebox", settingsWindowSource);
    }

    [Fact]
    public void App_starts_and_stops_overwork_reminder_service()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs");
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var app = File.ReadAllText(appPath);
        var lifecycle = File.ReadAllText(lifecyclePath);

        Assert.Contains("IOverworkReminderService", app);
        Assert.Contains("StartAsync(CancellationToken.None)", app);
        Assert.Contains("overwork reminder service", lifecycle);
        Assert.Contains("IOverworkReminderService", lifecycle);
        Assert.Contains("StopAsync(CancellationToken.None)", lifecycle);
    }

    [Fact]
    public void App_registers_overwork_reminder_toast_window()
    {
        var diPath = FindRepositoryFile("src", "GestureClip.App", "DependencyInjection", "AppServiceCollectionExtensions.cs");
        var di = File.ReadAllText(diPath);

        Assert.Contains("IOverworkReminderToastService", di);
        Assert.Contains("OverworkReminderToastService", di);
        Assert.Contains("OverworkReminderToastWindow", di);
    }
}
