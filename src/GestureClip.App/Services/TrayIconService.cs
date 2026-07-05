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
        _menu.Items.Add("打开设置", null, (_, _) => _appLifecycleService.ShowSettingsWindow());
        if (_settingsService.Get(SettingKeys.WorkstationEnabled, true))
        {
            _menu.Items.Add(CreateWorkBearMenu());
        }
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(
            snapshot.ClipboardCaptureEnabled ? "暂停剪贴板记录 10 分钟" : "恢复剪贴板记录",
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
            snapshot.GestureEnabled ? "暂停鼠标手势 10 分钟" : "恢复鼠标手势",
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
        _menu.Items.Add("查看日志", null, (_, _) => OpenDirectory(_paths.LogDirectory));
        _menu.Items.Add("打开数据目录", null, (_, _) => OpenDirectory(Path.GetDirectoryName(_paths.DatabasePath) ?? _paths.LogDirectory));
        _menu.Items.Add("导出诊断包", null, async (_, _) =>
        {
            var path = await _diagnosticsService.ExportPackageAsync(CancellationToken.None);
            ShowWorkBearBalloon("GestureClip", $"诊断包已导出：{path}");
        });
        _menu.Items.Add("一键更新", null, (_, _) => _appLifecycleService.OpenLatestReleasePage());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => _appLifecycleService.ExitApplication());
    }

    private Forms.ToolStripMenuItem CreateWorkBearMenu()
    {
        var root = new Forms.ToolStripMenuItem("工位小熊");
        var snapshot = _lastWorkBearSnapshot;

        root.DropDownItems.Add("打开工位小熊 Hub", null, (_, _) => _appLifecycleService.ShowWorkstationDashboardWindow());
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
        root.DropDownItems.Add("查看今日生存报告", null, async (_, _) =>
        {
            var report = await _workstationDashboardService.GenerateDailyReportAsync(DateTimeOffset.Now, CancellationToken.None);
            Forms.MessageBox.Show(report.ReportText, "工位小熊今日报告", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
        });
        root.DropDownItems.Add("生成分享卡片", null, async (_, _) =>
        {
            var path = await _workBearShareCardService.GenerateTodayCardAsync(CancellationToken.None);
            _notifyIcon?.ShowBalloonTip(3000, "工位小熊", $"今日生存报告已生成，里面不包含剪贴板内容：{path}", Forms.ToolTipIcon.Info);
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
