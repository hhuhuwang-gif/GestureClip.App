using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Diagnostics;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using GestureClip.Infrastructure.Paths;
using System.Windows.Threading;

namespace GestureClip.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly IMouseGestureService _mouseGestureService;
    private readonly IGestureSettingsProvider _gestureSettingsProvider;
    private readonly IFeatureToggleService _featureToggleService;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly IAppBlacklistService _appBlacklistService;
    private readonly IStartupService _startupService;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly IClipboardService _clipboardService;
    private readonly IClipboardWriter _clipboardWriter;
    private bool _clipboardCaptureEnabled;
    private bool _gestureEnabled;
    private bool _gestureShowOverlay;
    private bool _gestureDebugEnabled;
    private bool _gestureCloseWindowEnabled;
    private GesturePreset _selectedGesturePreset;
    private int _gestureTriggerThreshold;
    private GestureDiagnosticsSnapshot _gestureDiagnostics;
    private DiagnosticsSnapshot? _diagnostics;
    private string _newBlacklistProcessName = "";
    private bool _startWithWindows;
    private readonly DispatcherTimer _diagnosticsTimer;

    public SettingsViewModel(
        AppPathProvider paths,
        ISystemPermissionService permissionService,
        ISettingsService settingsService,
        IMouseGestureService mouseGestureService,
        IGestureSettingsProvider gestureSettingsProvider,
        IFeatureToggleService featureToggleService,
        IGlobalHotkeyService globalHotkeyService,
        IAppBlacklistService appBlacklistService,
        IStartupService startupService,
        IDiagnosticsService diagnosticsService,
        IClipboardService clipboardService,
        IClipboardWriter clipboardWriter)
    {
        _settingsService = settingsService;
        _mouseGestureService = mouseGestureService;
        _gestureSettingsProvider = gestureSettingsProvider;
        _featureToggleService = featureToggleService;
        _globalHotkeyService = globalHotkeyService;
        _appBlacklistService = appBlacklistService;
        _startupService = startupService;
        _diagnosticsService = diagnosticsService;
        _clipboardService = clipboardService;
        _clipboardWriter = clipboardWriter;
        DatabasePath = paths.DatabasePath;
        LogDirectory = paths.LogDirectory;
        AppDataPath = paths.RootDirectory;
        PermissionStatus = permissionService.GetCurrentStatus();
        var toggles = _featureToggleService.GetSnapshot();
        _clipboardCaptureEnabled = toggles.ClipboardCaptureEnabled;
        _gestureEnabled = toggles.GestureEnabled;
        _gestureShowOverlay = _settingsService.Get(SettingKeys.GestureShowOverlay, true);
        _gestureDebugEnabled = _settingsService.Get(SettingKeys.GestureDebugEnabled, false);
        _gestureCloseWindowEnabled = _settingsService.Get(SettingKeys.GestureCloseWindowEnabled, false);
        _selectedGesturePreset = _settingsService.Get(SettingKeys.GesturePreset, GesturePreset.EditEnhanced);
        _gestureTriggerThreshold = _settingsService.Get(SettingKeys.GestureTriggerThreshold, 20);
        _gestureDiagnostics = _mouseGestureService.Diagnostics;
        _startWithWindows = _startupService.IsEnabled();
        StartupModeWarning = _startupService.IsDevelopmentRunMode()
            ? "当前看起来是开发运行路径，开机自启建议在发布版中开启。"
            : "";
        AddBlacklistItemCommand = new AsyncRelayCommand(_ => AddBlacklistItemAsync());
        RefreshDiagnosticsCommand = new AsyncRelayCommand(_ => RefreshDiagnosticsAsync());
        CopyDiagnosticsCommand = new AsyncRelayCommand(_ => CopyDiagnosticsAsync());
        OpenLogDirectoryCommand = new RelayCommand(_ => OpenDirectory(LogDirectory));
        OpenDataDirectoryCommand = new RelayCommand(_ => OpenDirectory(AppDataPath));
        _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _diagnosticsTimer.Tick += (_, _) => RefreshGestureDiagnostics();
        _diagnosticsTimer.Start();
        _ = LoadBlacklistAsync();
        _ = RefreshDiagnosticsAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PermissionStatus PermissionStatus { get; }

    public string PermissionStatusText => PermissionStatus == PermissionStatus.Administrator
        ? "管理员权限"
        : "普通权限";

    public string DatabasePath { get; }

    public string LogDirectory { get; }

    public string AppDataPath { get; }

    public string StartupStatus => "托盘常驻已启用，关闭窗口将隐藏到托盘。";

    public string HotkeyStatusText => _globalHotkeyService.Status.DisplayText;

    public ObservableCollection<AppBlacklistItemViewModel> AppBlacklistItems { get; } = [];

    public ICommand AddBlacklistItemCommand { get; }

    public ICommand RefreshDiagnosticsCommand { get; }

    public ICommand CopyDiagnosticsCommand { get; }

    public ICommand OpenLogDirectoryCommand { get; }

    public ICommand OpenDataDirectoryCommand { get; }

    public string NewBlacklistProcessName
    {
        get => _newBlacklistProcessName;
        set
        {
            if (_newBlacklistProcessName == value)
            {
                return;
            }

            _newBlacklistProcessName = value;
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows == value)
            {
                return;
            }

            _startWithWindows = value;
            OnPropertyChanged();
            _ = ApplyStartWithWindowsAsync(value);
        }
    }

    public string StartupModeWarning { get; }

    public DiagnosticsSnapshot? Diagnostics
    {
        get => _diagnostics;
        private set
        {
            _diagnostics = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DiagnosticsText));
        }
    }

    public string DiagnosticsText
    {
        get
        {
            if (Diagnostics is null)
            {
                return "诊断信息加载中...";
            }

            return
                $"版本：{Diagnostics.AppVersion}\n" +
                $"剪贴板记录：{(Diagnostics.ClipboardCaptureEnabled ? "开启" : "暂停")}\n" +
                $"鼠标手势：{(Diagnostics.GestureEnabled ? "开启" : "暂停")}\n" +
                $"快捷键：{Diagnostics.HotkeyStatus}\n" +
                $"Hook：{Diagnostics.HookStatus}\n" +
                $"最近 Pattern：{Diagnostics.LastGesturePattern ?? "-"}\n" +
                $"最近 Action：{Diagnostics.LastGestureAction ?? "-"}\n" +
                $"最近键盘输入：{Diagnostics.LastKeyboardInputStatus ?? "-"}\n" +
                $"最近错误：{Diagnostics.LastErrorSummary ?? "-"}";
        }
    }

    public bool ClipboardCaptureEnabled
    {
        get => _clipboardCaptureEnabled;
        set
        {
            if (_clipboardCaptureEnabled == value)
            {
                return;
            }

            _clipboardCaptureEnabled = value;
            OnPropertyChanged();
            _ = ApplyClipboardCaptureEnabledAsync(value);
        }
    }

    public string GestureHookStatus => _gestureDiagnostics.HookStatus;

    public string GestureRuntimeStateText => _gestureDiagnostics.State.ToString();

    public string LastGesturePattern => string.IsNullOrWhiteSpace(_gestureDiagnostics.LastPattern)
        ? "-"
        : _gestureDiagnostics.LastPattern;

    public string LastGestureAction => _gestureDiagnostics.LastAction.ToString();

    public string LastGestureError => string.IsNullOrWhiteSpace(_gestureDiagnostics.LastError)
        ? "-"
        : _gestureDiagnostics.LastError;

    public string LastGestureEventTime => _gestureDiagnostics.LastEventAt is null
        ? "-"
        : _gestureDiagnostics.LastEventAt.Value.ToLocalTime().ToString("HH:mm:ss.fff");

    public string GestureEnvironmentStatus => _gestureDiagnostics.IsDisabledByEnvironment
        ? "手势被环境变量 GESTURECLIP_DISABLE_GESTURES=1 禁用"
        : "环境变量未禁用手势";

    public IReadOnlyList<GesturePresetOption> GesturePresetOptions { get; } =
    [
        new(GesturePreset.EditEnhanced, "编辑增强模式"),
        new(GesturePreset.ClipboardEnhanced, "剪贴板增强模式"),
        new(GesturePreset.Custom, "自定义模式")
    ];

    public GesturePresetOption? SelectedGesturePresetOption
    {
        get => GesturePresetOptions.FirstOrDefault(option => option.Value == _selectedGesturePreset);
        set
        {
            if (value is null || _selectedGesturePreset == value.Value)
            {
                return;
            }

            _selectedGesturePreset = value.Value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GesturePreset, _selectedGesturePreset, CancellationToken.None);
        }
    }

    public bool GestureEnabled
    {
        get => _gestureEnabled;
        set
        {
            if (_gestureEnabled == value)
            {
                return;
            }

            _gestureEnabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = ApplyGestureEnabledAsync(value);
        }
    }

    public bool GestureShowOverlay
    {
        get => _gestureShowOverlay;
        set
        {
            if (_gestureShowOverlay == value)
            {
                return;
            }

            _gestureShowOverlay = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureShowOverlay, value, CancellationToken.None);
        }
    }

    public bool GestureDebugEnabled
    {
        get => _gestureDebugEnabled;
        set
        {
            if (_gestureDebugEnabled == value)
            {
                return;
            }

            _gestureDebugEnabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureDebugEnabled, value, CancellationToken.None);
        }
    }

    public int GestureTriggerThreshold
    {
        get => _gestureTriggerThreshold;
        set
        {
            if (_gestureTriggerThreshold == value)
            {
                return;
            }

            _gestureTriggerThreshold = Math.Clamp(value, 4, 200);
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerThreshold, _gestureTriggerThreshold, CancellationToken.None);
        }
    }

    public bool GestureCloseWindowEnabled
    {
        get => _gestureCloseWindowEnabled;
        set
        {
            if (_gestureCloseWindowEnabled == value)
            {
                return;
            }

            _gestureCloseWindowEnabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureCloseWindowEnabled, value, CancellationToken.None);
        }
    }

    private async Task ApplyGestureEnabledAsync(bool enabled)
    {
        await _featureToggleService.SetGestureEnabledAsync(enabled, CancellationToken.None);
    }

    private async Task ApplyClipboardCaptureEnabledAsync(bool enabled)
    {
        await _featureToggleService.SetClipboardCaptureEnabledAsync(enabled, CancellationToken.None);
    }

    private async Task ApplyStartWithWindowsAsync(bool enabled)
    {
        if (enabled)
        {
            _startupService.Enable();
        }
        else
        {
            _startupService.Disable();
        }

        await _settingsService.SetAsync(SettingKeys.AppStartWithWindows, enabled, CancellationToken.None);
    }

    private async Task LoadBlacklistAsync()
    {
        var items = await _appBlacklistService.GetAllAsync(CancellationToken.None);
        AppBlacklistItems.Clear();
        foreach (var item in items)
        {
            AppBlacklistItems.Add(new AppBlacklistItemViewModel(item, UpdateBlacklistItemAsync, DeleteBlacklistItemAsync));
        }
    }

    private async Task AddBlacklistItemAsync()
    {
        await _appBlacklistService.AddAsync(NewBlacklistProcessName, blockClipboard: true, blockGesture: true, CancellationToken.None);
        NewBlacklistProcessName = "";
        await LoadBlacklistAsync();
    }

    private async Task UpdateBlacklistItemAsync(AppBlacklistItemViewModel item)
    {
        await _appBlacklistService.UpdateAsync(item.Id, item.BlockClipboard, item.BlockGesture, CancellationToken.None);
    }

    private async Task DeleteBlacklistItemAsync(AppBlacklistItemViewModel item)
    {
        await _appBlacklistService.DeleteAsync(item.Id, CancellationToken.None);
        AppBlacklistItems.Remove(item);
    }

    private async Task RefreshDiagnosticsAsync()
    {
        Diagnostics = await _diagnosticsService.GetSnapshotAsync(CancellationToken.None);
    }

    private async Task CopyDiagnosticsAsync()
    {
        var report = await _diagnosticsService.BuildReportAsync(CancellationToken.None);
        _clipboardService.SuppressCaptureFor(TimeSpan.FromMilliseconds(1000));
        await _clipboardWriter.SetTextAsync(report, CancellationToken.None);
    }

    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void UpdateGestureSettingsSnapshot()
    {
        _gestureSettingsProvider.Update(new GestureSettings(
            _gestureEnabled,
            _gestureShowOverlay,
            _gestureCloseWindowEnabled,
            _gestureDebugEnabled,
            _selectedGesturePreset,
            new GestureOptions(_gestureTriggerThreshold, 16, 2000, 2)));
    }

    private void RefreshGestureDiagnostics()
    {
        _gestureDiagnostics = _mouseGestureService.Diagnostics;
        OnPropertyChanged(nameof(GestureHookStatus));
        OnPropertyChanged(nameof(GestureRuntimeStateText));
        OnPropertyChanged(nameof(LastGesturePattern));
        OnPropertyChanged(nameof(LastGestureAction));
        OnPropertyChanged(nameof(LastGestureError));
        OnPropertyChanged(nameof(LastGestureEventTime));
        OnPropertyChanged(nameof(GestureEnvironmentStatus));
        OnPropertyChanged(nameof(HotkeyStatusText));
        _ = RefreshDiagnosticsAsync();
        var toggles = _featureToggleService.GetSnapshot();
        if (_clipboardCaptureEnabled != toggles.ClipboardCaptureEnabled)
        {
            _clipboardCaptureEnabled = toggles.ClipboardCaptureEnabled;
            OnPropertyChanged(nameof(ClipboardCaptureEnabled));
        }

        if (_gestureEnabled != toggles.GestureEnabled)
        {
            _gestureEnabled = toggles.GestureEnabled;
            OnPropertyChanged(nameof(GestureEnabled));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed record GesturePresetOption(GesturePreset Value, string DisplayName);
}
