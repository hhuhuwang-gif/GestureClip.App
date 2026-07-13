using System.Diagnostics;
using System.Windows;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.App.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    private const string LatestReleaseUrl = "https://github.com/hhuhuwang-gif/GestureClip.App/releases/latest";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppLifecycleService> _logger;
    private SettingsWindow? _settingsWindow;
    private WorkstationDashboardWindow? _workstationDashboardWindow;
    private int _exitStarted;

    public AppLifecycleService(IServiceProvider serviceProvider, ILogger<AppLifecycleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsExplicitExit { get; private set; }

    public bool RuntimeStopped { get; private set; }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
        _logger.LogInformation("Settings window shown.");
    }

    public void ToggleSettingsWindow()
    {
        if (_settingsWindow is not null &&
            _settingsWindow.IsVisible &&
            _settingsWindow.WindowState != WindowState.Minimized)
        {
            _settingsWindow.Hide();
            _logger.LogInformation("Settings window hidden to tray by app icon toggle.");
            return;
        }

        ShowSettingsWindow();
    }

    public void ShowWorkstationDashboardWindow()
    {
        if (_workstationDashboardWindow is null)
        {
            _workstationDashboardWindow = _serviceProvider.GetRequiredService<WorkstationDashboardWindow>();
            _workstationDashboardWindow.Closed += (_, _) => _workstationDashboardWindow = null;
        }

        _workstationDashboardWindow.Show();
        if (_workstationDashboardWindow.WindowState == WindowState.Minimized)
        {
            _workstationDashboardWindow.WindowState = WindowState.Normal;
        }

        _workstationDashboardWindow.Activate();
        _logger.LogInformation("Workstation dashboard window shown.");
    }

    public void OpenLatestReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = LatestReleaseUrl,
                UseShellExecute = true
            });
            _logger.LogInformation("Latest release page opened for update.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open latest release page.");
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await _serviceProvider.GetRequiredService<IUpdateCheckService>().CheckLatestAsync();
            var notes = TruncateReleaseNotes(result.ReleaseNotes);
            if (!result.IsUpdateAvailable)
            {
                var openRelease = System.Windows.MessageBox.Show(
                    $"当前已是最新版本。\n\n" +
                    $"当前版本：{result.CurrentVersion}\n" +
                    $"最新版本：{result.LatestVersion}\n" +
                    $"发布名：{result.ReleaseName}\n\n" +
                    $"更新说明：\n{notes}\n\n" +
                    "是否打开 Release 页面查看完整内容？",
                    "检查更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (openRelease == MessageBoxResult.Yes)
                {
                    OpenLatestReleasePage();
                }

                return;
            }

            var updateNow = System.Windows.MessageBox.Show(
                $"发现新版本：{result.LatestVersion}\n\n" +
                $"当前版本：{result.CurrentVersion}\n" +
                $"发布名：{result.ReleaseName}\n\n" +
                $"更新说明：\n{notes}\n\n" +
                "是否现在下载并覆盖安装？\n（本地剪贴板历史和设置会保留）",
                "发现新版本",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (updateNow == MessageBoxResult.Yes)
            {
                await StartCoverUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates.");
            var openRelease = System.Windows.MessageBox.Show(
                UpdateErrorMessageFormatter.ToUserMessage(ex) + "\n\n是否打开 GitHub Release 页面手动查看？",
                "检查更新失败",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (openRelease == MessageBoxResult.Yes)
            {
                OpenLatestReleasePage();
            }
        }
    }

    private static string TruncateReleaseNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return "（暂无更新说明）";
        }

        var normalized = notes.Replace("\r\n", "\n").Trim();
        const int max = 900;
        return normalized.Length <= max
            ? normalized
            : normalized[..max] + "\n…(已截断，完整说明见 Release 页面)";
    }

    public async Task StartCoverUpdateAsync()
    {
        try
        {
            var result = System.Windows.MessageBox.Show(
                "将下载 GitHub 最新版本，关闭当前 GestureClip 后自动覆盖旧程序并重启。\n\n" +
                "下载时会自动尝试系统代理、直连与镜像加速，提高成功率。\n" +
                "本地剪贴板历史和设置会保留。是否继续？",
                "一键覆盖更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            await _serviceProvider.GetRequiredService<IUpdateInstallerService>().StartCoverUpdateAsync();
            _logger.LogInformation("Cover update requested; shutting down current app instance.");
            ExitApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start cover update.");
            System.Windows.MessageBox.Show(
                "自动更新启动失败。" + UpdateErrorMessageFormatter.ToUserMessage(ex),
                "一键更新失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            OpenLatestReleasePage();
        }
    }

    public async void ExitApplication()
    {
        if (Interlocked.Exchange(ref _exitStarted, 1) == 1)
        {
            return;
        }

        IsExplicitExit = true;
        _logger.LogInformation("Explicit application exit requested.");
        await StopRuntimeAsync();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    public async Task StopRuntimeAsync()
    {
        if (RuntimeStopped)
        {
            return;
        }

        RuntimeStopped = true;
        await StopStepAsync("global hotkey", () =>
        {
            _serviceProvider.GetService<IGlobalHotkeyService>()?.Stop();
            return Task.CompletedTask;
        });
        await StopStepAsync("edge trigger service", () =>
            _serviceProvider.GetService<IEdgeTriggerService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("mouse gesture service", () =>
            _serviceProvider.GetService<IMouseGestureService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("clipboard service", () =>
            _serviceProvider.GetService<IClipboardService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("overwork reminder service", () =>
            _serviceProvider.GetService<IOverworkReminderService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("workbear daily report service", () =>
            _serviceProvider.GetService<WorkBearDailyReportAutoService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("tray icon", () =>
        {
            _serviceProvider.GetService<TrayIconService>()?.Dispose();
            return Task.CompletedTask;
        });
    }

    private async Task StopStepAsync(string name, Func<Task> stopAsync)
    {
        try
        {
            await stopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            _logger.LogInformation("Stopped {RuntimeComponent}.", name);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timed out while stopping {RuntimeComponent}; continuing shutdown.", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop {RuntimeComponent}; continuing shutdown.", name);
        }
    }
}

