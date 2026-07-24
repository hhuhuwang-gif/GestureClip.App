using System.Drawing;
using System.Diagnostics;
using System.IO;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using GestureClip.Infrastructure.Paths;
using Microsoft.Extensions.Logging;
using Forms = System.Windows.Forms;

namespace GestureClip.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly IAppLifecycleService _appLifecycleService;
    private readonly ClipboardOverlayService _clipboardOverlayService;
    private readonly IQuickActionCenterService _quickActionCenterService;
    private readonly IFeatureToggleService _featureToggleService;
    private readonly ISettingsService _settingsService;
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly IWorkBearShareCardService _workBearShareCardService;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly AppPathProvider _paths;
    private readonly ILogger<TrayIconService> _logger;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _menu;
    private WorkstationDashboardSnapshot? _lastWorkBearSnapshot;

    public TrayIconService(
        IAppLifecycleService appLifecycleService,
        ClipboardOverlayService clipboardOverlayService,
        IQuickActionCenterService quickActionCenterService,
        IFeatureToggleService featureToggleService,
        ISettingsService settingsService,
        IWorkstationDashboardService workstationDashboardService,
        IWorkBearShareCardService workBearShareCardService,
        IDiagnosticsService diagnosticsService,
        AppPathProvider paths,
        ILogger<TrayIconService> logger)
    {
        _appLifecycleService = appLifecycleService;
        _clipboardOverlayService = clipboardOverlayService;
        _quickActionCenterService = quickActionCenterService;
        _featureToggleService = featureToggleService;
        _settingsService = settingsService;
        _workstationDashboardService = workstationDashboardService;
        _workBearShareCardService = workBearShareCardService;
        _diagnosticsService = diagnosticsService;
        _paths = paths;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _menu = new Forms.ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();
        RebuildMenu();

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "GestureClip",
            Icon = LoadAppIcon(),
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _appLifecycleService.ToggleSettingsWindow();
            }
        };

        _logger.LogInformation("Tray icon initialized.");
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _menu?.Dispose();
        _menu = null;
    }

    public void ShowWorkBearBalloon(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info);
    }

    private void RebuildMenu()
    {
        if (_menu is null)
        {
            return;
        }

        _menu.Items.Clear();
        var snapshot = _featureToggleService.GetSnapshot();

        _menu.Items.Add("打开剪贴板历史", null, async (_, _) => await _clipboardOverlayService.ShowAsync());
        if (_settingsService.Get(SettingKeys.AssistantEnabled, true))
        {
            _menu.Items.Add("打开快捷动作 (Ctrl+Shift+Q)", null, async (_, _) => await _quickActionCenterService.ShowAsync());
        }

        _menu.Items.Add("打开设置", null, (_, _) => _appLifecycleService.ShowSettingsWindow());
        if (_settingsService.Get(SettingKeys.WorkstationEnabled, true))
        {
            _menu.Items.Add(CreateWorkBearMenu());
        }
        _menu.Items.Add(new Forms.ToolStripSeparator());
        // Quick permanent toggles
        _menu.Items.Add(
            snapshot.ClipboardCaptureEnabled ? "✓ 剪贴板记录：开" : "剪贴板记录：关",
            null,
            async (_, _) =>
            {
                await _featureToggleService.SetClipboardCaptureEnabledAsync(!snapshot.ClipboardCaptureEnabled, CancellationToken.None);
                ShowWorkBearBalloon("GestureClip", snapshot.ClipboardCaptureEnabled ? "剪贴板记录已关闭。" : "剪贴板记录已开启。");
            });
        _menu.Items.Add(
            snapshot.GestureEnabled ? "✓ 鼠标手势：开" : "鼠标手势：关",
            null,
            async (_, _) =>
            {
                await _featureToggleService.SetGestureEnabledAsync(!snapshot.GestureEnabled, CancellationToken.None);
                ShowWorkBearBalloon("GestureClip", snapshot.GestureEnabled ? "鼠标手势已关闭。" : "鼠标手势已开启。");
            });
        var bothOn = snapshot.ClipboardCaptureEnabled && snapshot.GestureEnabled;
        _menu.Items.Add(
            bothOn ? "暂停全部功能" : "恢复全部功能",
            null,
            async (_, _) =>
            {
                var enable = !bothOn;
                await _featureToggleService.SetClipboardCaptureEnabledAsync(enable, CancellationToken.None);
                await _featureToggleService.SetGestureEnabledAsync(enable, CancellationToken.None);
                ShowWorkBearBalloon("GestureClip", enable ? "剪贴板与手势已恢复。" : "剪贴板与手势已全部暂停。");
            });
        _menu.Items.Add(
            snapshot.ClipboardCaptureEnabled ? "暂停剪贴板 10 分钟" : "恢复剪贴板记录",
            null,
            async (_, _) =>
            {
                if (snapshot.ClipboardCaptureEnabled)
                {
                    _ = PauseClipboardCaptureForAsync(TimeSpan.FromMinutes(10));
                }
                else
                {
                    await _featureToggleService.SetClipboardCaptureEnabledAsync(true, CancellationToken.None);
                }
            });
        _menu.Items.Add(
            snapshot.GestureEnabled ? "暂停手势 10 分钟" : "恢复鼠标手势",
            null,
            async (_, _) =>
            {
                if (snapshot.GestureEnabled)
                {
                    _ = PauseGestureForAsync(TimeSpan.FromMinutes(10));
                }
                else
                {
                    await _featureToggleService.SetGestureEnabledAsync(true, CancellationToken.None);
                }
            });
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("卸载 GestureClip…", null, (_, _) => LaunchUninstall());
        _menu.Items.Add("查看日志", null, (_, _) => OpenDirectory(_paths.LogDirectory));
        _menu.Items.Add("打开数据目录", null, (_, _) => OpenDirectory(Path.GetDirectoryName(_paths.DatabasePath) ?? _paths.LogDirectory));
        _menu.Items.Add("导出诊断包", null, async (_, _) =>
        {
            var path = await _diagnosticsService.ExportPackageAsync(CancellationToken.None);
            ShowWorkBearBalloon("GestureClip", $"诊断包已导出：{path}");
        });
        _menu.Items.Add("检查更新", null, async (_, _) => await _appLifecycleService.CheckForUpdatesAsync());
        _menu.Items.Add("一键覆盖更新", null, async (_, _) => await _appLifecycleService.StartCoverUpdateAsync());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => _appLifecycleService.ExitApplication());
    }

    private Forms.ToolStripMenuItem CreateWorkBearMenu()
    {
        var root = new Forms.ToolStripMenuItem("工位小熊");
        var snapshot = _lastWorkBearSnapshot;

        root.DropDownItems.Add("打开工位小熊 Hub", null, (_, _) => _appLifecycleService.ShowWorkstationDashboardWindow());
        root.DropDownItems.Add("今日已赚摘要", null, async (_, _) =>
        {
            var latest = await GetWorkBearSnapshotSafeAsync();
            if (latest is null)
            {
                ShowWorkBearBalloon("工位小熊", "暂时读不到状态，请稍后重试。");
                return;
            }

            var off = latest.TimeUntilOffWork <= TimeSpan.Zero
                ? "已下班"
                : $"{(int)latest.TimeUntilOffWork.TotalHours:D2}:{latest.TimeUntilOffWork.Minutes:D2}";
            ShowWorkBearBalloon(
                "工位小熊 · 今日摘要",
                $"今日已赚 ￥{latest.TodayEarned:F2}\n距离下班 {off}\n{latest.BearStatusText} · {latest.BearLineText}");
        });
        var fishingItem = root.DropDownItems.Add(snapshot?.IsFishing == true ? "结束摸鱼" : "开始摸鱼", null, async (_, _) =>
        {
            var latest = await GetWorkBearSnapshotSafeAsync();
            if (latest?.IsFishing == true)
            {
                await _workstationDashboardService.EndFishingAsync(DateTimeOffset.Now, CancellationToken.None);
            }
            else
            {
                await _workstationDashboardService.StartFishingAsync(DateTimeOffset.Now, CancellationToken.None);
            }

            _lastWorkBearSnapshot = await GetWorkBearSnapshotSafeAsync();
        });
        var sprintItem = root.DropDownItems.Add(snapshot?.SprintActive == true ? "关闭下班冲刺" : "开启下班冲刺", null, async (_, _) =>
        {
            var latest = await GetWorkBearSnapshotSafeAsync();
            var enable = latest?.SprintActive != true;
            await _workstationDashboardService.SetSprintModeAsync(enable, CancellationToken.None);
            _lastWorkBearSnapshot = await GetWorkBearSnapshotSafeAsync();
            ShowWorkBearBalloon("工位小熊", enable ? "下班冲刺已开启。" : "下班冲刺已关闭。");
        });
        root.DropDownItems.Add("查看今日生存报告", null, async (_, _) =>
        {
            var report = await _workstationDashboardService.GenerateDailyReportAsync(DateTimeOffset.Now, CancellationToken.None);
            Forms.MessageBox.Show(report.ReportText, "工位小熊今日报告", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
        });
        root.DropDownItems.Add("生成本周总结", null, async (_, _) =>
        {
            var text = await _workstationDashboardService.GeneratePeriodReportAsync(DateTimeOffset.Now, 7, CancellationToken.None);
            Forms.MessageBox.Show(text, "工位小熊 · 本周总结", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
        });
        root.DropDownItems.Add("生成分享卡片", null, async (_, _) =>
        {
            var path = await _workBearShareCardService.GenerateTodayCardAsync(CancellationToken.None);
            _notifyIcon?.ShowBalloonTip(4000, "工位小熊", $"卡片已生成（不含剪贴板内容）：{path}", Forms.ToolTipIcon.Info);
            _workBearShareCardService.OpenCardFolder(path);
        });
        var restItem = root.DropDownItems.Add(snapshot?.RestReminderEnabled != false ? "关闭休息提醒" : "开启休息提醒", null, async (_, _) =>
        {
            var latest = await GetWorkBearSnapshotSafeAsync();
            await _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, !(latest?.RestReminderEnabled ?? true), CancellationToken.None);
            _lastWorkBearSnapshot = await GetWorkBearSnapshotSafeAsync();
        });

        root.DropDownOpening += async (_, _) =>
        {
            var latest = await GetWorkBearSnapshotSafeAsync();
            if (latest is null)
            {
                return;
            }

            fishingItem.Text = latest.IsFishing ? "结束摸鱼" : "开始摸鱼";
            sprintItem.Text = latest.SprintActive ? "关闭下班冲刺" : "开启下班冲刺";
            restItem.Text = latest.RestReminderEnabled ? "关闭休息提醒" : "开启休息提醒";
        };

        return root;
    }

    private async Task<WorkstationDashboardSnapshot?> GetWorkBearSnapshotSafeAsync()
    {
        try
        {
            _lastWorkBearSnapshot = await _workstationDashboardService.GetSnapshotAsync(DateTimeOffset.Now, CancellationToken.None);
            return _lastWorkBearSnapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh WorkBear tray state.");
            return _lastWorkBearSnapshot;
        }
    }

    private async Task PauseClipboardCaptureForAsync(TimeSpan duration)
    {
        await _featureToggleService.SetClipboardCaptureEnabledAsync(false, CancellationToken.None);
        ShowWorkBearBalloon("GestureClip", "剪贴板记录已暂停 10 分钟。");
        await Task.Delay(duration);
        await _featureToggleService.SetClipboardCaptureEnabledAsync(true, CancellationToken.None);
        ShowWorkBearBalloon("GestureClip", "剪贴板记录已恢复。");
    }

    private async Task PauseGestureForAsync(TimeSpan duration)
    {
        await _featureToggleService.SetGestureEnabledAsync(false, CancellationToken.None);
        ShowWorkBearBalloon("GestureClip", "鼠标手势已暂停 10 分钟。");
        await Task.Delay(duration);
        await _featureToggleService.SetGestureEnabledAsync(true, CancellationToken.None);
        ShowWorkBearBalloon("GestureClip", "鼠标手势已恢复。");
    }


    private void LaunchUninstall()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GestureClip", "uninstall.ps1"),
                Path.Combine(AppContext.BaseDirectory, "uninstall.ps1"),
            };
            var uninstall = candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
            if (uninstall is null)
            {
                var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var result = Forms.MessageBox.Show(
                    "未找到卸载脚本（便携版可直接删除程序文件夹）。\n\n是否打开当前程序目录？\n\n用户数据在 %LOCALAPPDATA%\\GestureClip\\，删除程序不会自动清除。",
                    "卸载 GestureClip",
                    Forms.MessageBoxButtons.YesNo,
                    Forms.MessageBoxIcon.Question);
                if (result == Forms.DialogResult.Yes)
                {
                    OpenDirectory(dir);
                }
                return;
            }

            var confirm = Forms.MessageBox.Show(
                "将卸载程序文件（默认不会删除 %LOCALAPPDATA%\\GestureClip 用户数据）。\n\n是否继续？",
                "卸载 GestureClip",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Warning);
            if (confirm != Forms.DialogResult.Yes)
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{uninstall}\" -Uninstall",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(uninstall) ?? Environment.CurrentDirectory
            });
            _appLifecycleService.ExitApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch uninstall.");
            Forms.MessageBox.Show("无法启动卸载：" + ex.Message, "GestureClip", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
    }

    private void OpenDirectory(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open directory from tray menu.");
        }
    }

    private static Icon LoadAppIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/GestureClip.ico"));

        if (resource is null)
        {
            return SystemIcons.Application;
        }

        using var stream = resource.Stream;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
