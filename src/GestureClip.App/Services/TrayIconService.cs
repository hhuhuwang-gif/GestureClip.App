using System.Drawing;
using System.Diagnostics;
using System.IO;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
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
    private readonly AppPathProvider _paths;
    private readonly ILogger<TrayIconService> _logger;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _menu;

    public TrayIconService(
        IAppLifecycleService appLifecycleService,
        ClipboardOverlayService clipboardOverlayService,
        IFeatureToggleService featureToggleService,
        ISettingsService settingsService,
        AppPathProvider paths,
        ILogger<TrayIconService> logger)
    {
        _appLifecycleService = appLifecycleService;
        _clipboardOverlayService = clipboardOverlayService;
        _featureToggleService = featureToggleService;
        _settingsService = settingsService;
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

    private void RebuildMenu()
    {
        if (_menu is null)
        {
            return;
        }

        _menu.Items.Clear();
        var snapshot = _featureToggleService.GetSnapshot();

        if (_settingsService.Get(SettingKeys.WorkstationEnabled, true))
        {
            _menu.Items.Add("工位小熊", null, (_, _) => _appLifecycleService.ShowWorkstationDashboardWindow());
        }

        _menu.Items.Add("打开剪贴板历史", null, async (_, _) => await _clipboardOverlayService.ShowAsync());
        _menu.Items.Add("打开设置", null, (_, _) => _appLifecycleService.ShowSettingsWindow());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(
            snapshot.ClipboardCaptureEnabled ? "剪贴板记录：已开启" : "剪贴板记录：已暂停",
            null,
            async (_, _) => await _featureToggleService.ToggleClipboardCaptureAsync(CancellationToken.None));
        _menu.Items.Add(
            snapshot.GestureEnabled ? "鼠标手势：已开启" : "鼠标手势：已暂停",
            null,
            async (_, _) => await _featureToggleService.ToggleGestureAsync(CancellationToken.None));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("查看日志", null, (_, _) => OpenDirectory(_paths.LogDirectory));
        _menu.Items.Add("打开数据目录", null, (_, _) => OpenDirectory(Path.GetDirectoryName(_paths.DatabasePath) ?? _paths.LogDirectory));
        _menu.Items.Add("一键更新", null, (_, _) => _appLifecycleService.OpenLatestReleasePage());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => _appLifecycleService.ExitApplication());
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
