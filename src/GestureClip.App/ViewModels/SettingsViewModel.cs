using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private readonly IClipboardRepository _clipboardRepository;
    private readonly IClipboardOverlayService _clipboardOverlayService;
    private readonly IConfirmationService _confirmationService;
    private readonly IGesturePresetProvider _gesturePresetProvider;
    private readonly IEdgeTriggerService _edgeTriggerService;
    private bool _clipboardCaptureEnabled;
    private bool _gestureEnabled;
    private bool _gestureShowOverlay;
    private bool _gestureDebugEnabled;
    private bool _gestureCloseWindowEnabled;
    private bool _gestureMiddleButtonEnabled;
    private bool _gestureXButton1Enabled;
    private bool _gestureXButton2Enabled;
    private bool _edgeTriggerEnabled;
    private BuiltInGestureAction _edgeTriggerTopLeftAction;
    private BuiltInGestureAction _edgeTriggerTopRightAction;
    private BuiltInGestureAction _edgeTriggerBottomRightAction;
    private BuiltInGestureAction _edgeTriggerBottomLeftAction;
    private GesturePreset _selectedGesturePreset;
    private int _gestureTriggerThreshold;
    private GestureDiagnosticsSnapshot _gestureDiagnostics;
    private DiagnosticsSnapshot? _diagnostics;
    private string _newBlacklistProcessName = "";
    private bool _startWithWindows;
    private int _clipboardItemCount;
    private int _clipboardMaxItems;
    private int _clipboardRetentionDays;
    private string _gestureStrokeColor;
    private string _newGesturePattern = "";
    private BuiltInGestureAction _newGestureAction = BuiltInGestureAction.Copy;
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
        IClipboardWriter clipboardWriter,
        IClipboardRepository clipboardRepository,
        IClipboardOverlayService clipboardOverlayService,
        IConfirmationService confirmationService,
        IGesturePresetProvider gesturePresetProvider,
        IEdgeTriggerService edgeTriggerService)
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
        _clipboardRepository = clipboardRepository;
        _clipboardOverlayService = clipboardOverlayService;
        _confirmationService = confirmationService;
        _gesturePresetProvider = gesturePresetProvider;
        _edgeTriggerService = edgeTriggerService;
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
        _gestureMiddleButtonEnabled = _settingsService.Get(SettingKeys.GestureTriggerMiddleButtonEnabled, false);
        _gestureXButton1Enabled = _settingsService.Get(SettingKeys.GestureTriggerXButton1Enabled, false);
        _gestureXButton2Enabled = _settingsService.Get(SettingKeys.GestureTriggerXButton2Enabled, false);
        _edgeTriggerEnabled = _settingsService.Get(SettingKeys.EdgeTriggerEnabled, false);
        _edgeTriggerTopLeftAction = _settingsService.Get(SettingKeys.EdgeTriggerTopLeftAction, BuiltInGestureAction.StartMenu);
        _edgeTriggerTopRightAction = _settingsService.Get(SettingKeys.EdgeTriggerTopRightAction, BuiltInGestureAction.TaskSwitcher);
        _edgeTriggerBottomRightAction = _settingsService.Get(SettingKeys.EdgeTriggerBottomRightAction, BuiltInGestureAction.ShowDesktop);
        _edgeTriggerBottomLeftAction = _settingsService.Get(SettingKeys.EdgeTriggerBottomLeftAction, BuiltInGestureAction.SwitchApp);
        _selectedGesturePreset = _settingsService.Get(SettingKeys.GesturePreset, GesturePreset.EditEnhanced);
        _gestureTriggerThreshold = _settingsService.Get(SettingKeys.GestureTriggerThreshold, 20);
        _clipboardMaxItems = _settingsService.Get(SettingKeys.ClipboardMaxItems, 1000);
        _clipboardRetentionDays = _settingsService.Get(SettingKeys.ClipboardRetentionDays, 30);
        _gestureStrokeColor = _settingsService.Get(SettingKeys.GestureStrokeColor, "#8CC8FF");
        _gestureDiagnostics = _mouseGestureService.Diagnostics;
        _startWithWindows = _startupService.IsEnabled();
        StartupModeWarning = _startupService.IsDevelopmentRunMode()
            ? "当前看起来是开发运行路径，开机自启建议在发布版中开启。"
            : "";
        AddBlacklistItemCommand = new AsyncRelayCommand(_ => AddBlacklistItemAsync());
        RefreshDiagnosticsCommand = new AsyncRelayCommand(_ => RefreshDiagnosticsAsync());
        CopyDiagnosticsCommand = new AsyncRelayCommand(_ => CopyDiagnosticsAsync());
        RefreshClipboardStatsCommand = new AsyncRelayCommand(_ => RefreshClipboardStatsAsync());
        ClearAllClipboardItemsCommand = new AsyncRelayCommand(_ => ClearAllClipboardItemsAsync());
        ClearUnpinnedClipboardItemsCommand = new AsyncRelayCommand(_ => ClearUnpinnedClipboardItemsAsync());
        ApplyClipboardCleanupCommand = new AsyncRelayCommand(_ => ApplyClipboardCleanupAsync());
        AddCustomGestureBindingCommand = new AsyncRelayCommand(_ => AddCustomGestureBindingAsync());
        OpenLogDirectoryCommand = new RelayCommand(_ => OpenDirectory(LogDirectory));
        OpenDataDirectoryCommand = new RelayCommand(_ => OpenDirectory(AppDataPath));
        _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _diagnosticsTimer.Tick += (_, _) => RefreshGestureDiagnostics();
        _diagnosticsTimer.Start();
        _ = LoadBlacklistAsync();
        _ = RefreshDiagnosticsAsync();
        _ = RefreshClipboardStatsAsync();
        RefreshGestureBindingCards();
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

    public ObservableCollection<GestureBindingCardViewModel> GestureBindingCards { get; } = [];

    public IReadOnlyList<GestureTriggerModeViewModel> GestureTriggerModes { get; } =
    [
        new("鼠标右键", "已启用", true),
        new("鼠标中键", "可启用", false),
        new("鼠标左键", "预留", false),
        new("鼠标侧键 1", "可启用", false),
        new("鼠标侧键 2", "可启用", false),
        new("屏幕四角热区", "可启用", false),
        new("屏幕左边缘 + 鼠标中键", "后续", false),
        new("屏幕左边缘 + 鼠标左键", "后续", false),
        new("屏幕左边缘 + 鼠标侧键 1", "后续", false),
        new("屏幕左边缘 + 鼠标侧键 2", "后续", false),
        new("屏幕右上角 + 鼠标碰撞", "已支持", true),
        new("屏幕右上角 + 滚轮", "预留", false)
    ];

    public ICommand AddBlacklistItemCommand { get; }

    public ICommand RefreshDiagnosticsCommand { get; }

    public ICommand CopyDiagnosticsCommand { get; }

    public ICommand OpenLogDirectoryCommand { get; }

    public ICommand OpenDataDirectoryCommand { get; }

    public ICommand RefreshClipboardStatsCommand { get; }

    public ICommand ClearAllClipboardItemsCommand { get; }

    public ICommand ClearUnpinnedClipboardItemsCommand { get; }

    public ICommand ApplyClipboardCleanupCommand { get; }

    public ICommand AddCustomGestureBindingCommand { get; }

    public int ClipboardItemCount
    {
        get => _clipboardItemCount;
        private set
        {
            if (_clipboardItemCount == value)
            {
                return;
            }

            _clipboardItemCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ClipboardItemCountText));
        }
    }

    public string ClipboardItemCountText => $"当前历史数量：{ClipboardItemCount} 条";

    public IReadOnlyList<int> ClipboardMaxItemOptions { get; } = [100, 500, 1000, 5000];

    public int ClipboardMaxItems
    {
        get => _clipboardMaxItems;
        set
        {
            if (_clipboardMaxItems == value)
            {
                return;
            }

            _clipboardMaxItems = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.ClipboardMaxItems, value, CancellationToken.None);
        }
    }

    public int ClipboardRetentionDays
    {
        get => _clipboardRetentionDays;
        private set
        {
            if (_clipboardRetentionDays == value)
            {
                return;
            }

            _clipboardRetentionDays = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedClipboardRetentionOption));
        }
    }

    public IReadOnlyList<RetentionOption> ClipboardRetentionOptions { get; } =
    [
        new("7 天", 7),
        new("30 天", 30),
        new("90 天", 90),
        new("永久", 0)
    ];

    public RetentionOption? SelectedClipboardRetentionOption
    {
        get => ClipboardRetentionOptions.FirstOrDefault(option => option.Days == ClipboardRetentionDays);
        set
        {
            if (value is null || ClipboardRetentionDays == value.Days)
            {
                return;
            }

            ClipboardRetentionDays = value.Days;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.ClipboardRetentionDays, value.Days, CancellationToken.None);
        }
    }

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
            RefreshGestureBindingCards();
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

    public bool GestureMiddleButtonEnabled
    {
        get => _gestureMiddleButtonEnabled;
        set
        {
            if (_gestureMiddleButtonEnabled == value)
            {
                return;
            }

            _gestureMiddleButtonEnabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerMiddleButtonEnabled, value, CancellationToken.None);
        }
    }

    public bool GestureXButton1Enabled
    {
        get => _gestureXButton1Enabled;
        set
        {
            if (_gestureXButton1Enabled == value)
            {
                return;
            }

            _gestureXButton1Enabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerXButton1Enabled, value, CancellationToken.None);
        }
    }

    public bool GestureXButton2Enabled
    {
        get => _gestureXButton2Enabled;
        set
        {
            if (_gestureXButton2Enabled == value)
            {
                return;
            }

            _gestureXButton2Enabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerXButton2Enabled, value, CancellationToken.None);
        }
    }

    public bool EdgeTriggerEnabled
    {
        get => _edgeTriggerEnabled;
        set
        {
            if (_edgeTriggerEnabled == value)
            {
                return;
            }

            _edgeTriggerEnabled = value;
            OnPropertyChanged();
            _ = ApplyEdgeTriggerEnabledAsync(value);
        }
    }

    public BuiltInGestureAction EdgeTriggerTopLeftAction
    {
        get => _edgeTriggerTopLeftAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerTopLeftAction, value, SettingKeys.EdgeTriggerTopLeftAction);
    }

    public BuiltInGestureAction EdgeTriggerTopRightAction
    {
        get => _edgeTriggerTopRightAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerTopRightAction, value, SettingKeys.EdgeTriggerTopRightAction);
    }

    public BuiltInGestureAction EdgeTriggerBottomRightAction
    {
        get => _edgeTriggerBottomRightAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerBottomRightAction, value, SettingKeys.EdgeTriggerBottomRightAction);
    }

    public BuiltInGestureAction EdgeTriggerBottomLeftAction
    {
        get => _edgeTriggerBottomLeftAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerBottomLeftAction, value, SettingKeys.EdgeTriggerBottomLeftAction);
    }

    public IReadOnlyList<GestureStrokeColorOption> GestureStrokeColorOptions { get; } =
    [
        new("冰蓝", "#8CC8FF"),
        new("青绿", "#6EE7D8"),
        new("紫光", "#C4B5FD"),
        new("粉色", "#FDA4C8"),
        new("琥珀", "#FCD34D"),
        new("白色", "#F8FAFC")
    ];

    public GestureStrokeColorOption? SelectedGestureStrokeColorOption
    {
        get => GestureStrokeColorOptions.FirstOrDefault(option => string.Equals(option.Color, _gestureStrokeColor, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || string.Equals(_gestureStrokeColor, value.Color, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _gestureStrokeColor = value.Color;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.GestureStrokeColor, value.Color, CancellationToken.None);
        }
    }

    public string NewGesturePattern
    {
        get => _newGesturePattern;
        set
        {
            var normalized = NormalizeGesturePattern(value);
            if (_newGesturePattern == normalized)
            {
                return;
            }

            _newGesturePattern = normalized;
            OnPropertyChanged();
        }
    }

    public BuiltInGestureAction NewGestureAction
    {
        get => _newGestureAction;
        set
        {
            if (_newGestureAction == value)
            {
                return;
            }

            _newGestureAction = value;
            OnPropertyChanged();
        }
    }

    private async Task ApplyGestureEnabledAsync(bool enabled)
    {
        await _featureToggleService.SetGestureEnabledAsync(enabled, CancellationToken.None);
    }

    private async Task ApplyEdgeTriggerEnabledAsync(bool enabled)
    {
        await _settingsService.SetAsync(SettingKeys.EdgeTriggerEnabled, enabled, CancellationToken.None);
        if (enabled)
        {
            await _edgeTriggerService.StartAsync(CancellationToken.None);
        }
        else
        {
            await _edgeTriggerService.StopAsync(CancellationToken.None);
        }
    }

    private async Task ApplyClipboardCaptureEnabledAsync(bool enabled)
    {
        await _featureToggleService.SetClipboardCaptureEnabledAsync(enabled, CancellationToken.None);
    }

    public async Task RefreshClipboardStatsAsync()
    {
        ClipboardItemCount = await _clipboardRepository.GetCountAsync(CancellationToken.None);
    }

    public async Task ClearAllClipboardItemsAsync()
    {
        if (!_confirmationService.Confirm(
            "清空全部剪贴板历史",
            "这会删除所有剪贴板历史，包括固定项。是否继续？"))
        {
            await RefreshClipboardStatsAsync();
            return;
        }

        await _clipboardRepository.ClearAllAsync(CancellationToken.None);
        await RefreshAfterClipboardCleanupAsync();
    }

    public async Task ClearUnpinnedClipboardItemsAsync()
    {
        if (!_confirmationService.Confirm(
            "清空非固定项",
            "这会删除所有非固定剪贴板历史，固定项会保留。是否继续？"))
        {
            await RefreshClipboardStatsAsync();
            return;
        }

        await _clipboardRepository.ClearUnpinnedAsync(CancellationToken.None);
        await RefreshAfterClipboardCleanupAsync();
    }

    public async Task ApplyClipboardCleanupAsync()
    {
        if (!_confirmationService.Confirm(
            "立即执行剪贴板清理",
            "这会根据最大保存数量和保留天数删除旧的非固定剪贴板记录。是否继续？"))
        {
            await RefreshClipboardStatsAsync();
            return;
        }

        await _clipboardRepository.CleanupAsync(ClipboardMaxItems, ClipboardRetentionDays, CancellationToken.None);
        await RefreshAfterClipboardCleanupAsync();
    }

    private async Task RefreshAfterClipboardCleanupAsync()
    {
        await RefreshClipboardStatsAsync();
        await _clipboardOverlayService.RefreshAsync();
    }

    private async Task ApplyStartWithWindowsAsync(bool enabled)
    {
        if (enabled)
        {
            if (_startupService.IsDevelopmentRunMode())
            {
                _startWithWindows = false;
                OnPropertyChanged(nameof(StartWithWindows));
                return;
            }

            _startupService.Enable();
        }
        else
        {
            _startupService.Disable();
        }

        await _settingsService.SetAsync(SettingKeys.AppStartWithWindows, enabled, CancellationToken.None);
    }

    private void SetEdgeTriggerAction(ref BuiltInGestureAction field, BuiltInGestureAction value, string settingKey)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(settingKey switch
        {
            SettingKeys.EdgeTriggerTopLeftAction => nameof(EdgeTriggerTopLeftAction),
            SettingKeys.EdgeTriggerTopRightAction => nameof(EdgeTriggerTopRightAction),
            SettingKeys.EdgeTriggerBottomRightAction => nameof(EdgeTriggerBottomRightAction),
            SettingKeys.EdgeTriggerBottomLeftAction => nameof(EdgeTriggerBottomLeftAction),
            _ => null
        });
        _ = _settingsService.SetAsync(settingKey, value, CancellationToken.None);
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
            new GestureOptions(_gestureTriggerThreshold, 16, 2000, 2),
            _gestureMiddleButtonEnabled,
            _gestureXButton1Enabled,
            _gestureXButton2Enabled));
    }

    private void RefreshGestureBindingCards()
    {
        GestureBindingCards.Clear();
        var bindings = _gesturePresetProvider.GetBindings(_selectedGesturePreset);
        var patterns = GesturePatterns
            .Concat(bindings.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(pattern => Array.IndexOf(GesturePatterns, pattern) >= 0 ? Array.IndexOf(GesturePatterns, pattern) : int.MaxValue)
            .ThenBy(pattern => pattern, StringComparer.Ordinal);

        foreach (var pattern in patterns)
        {
            var action = bindings.TryGetValue(pattern, out var mappedAction) ? mappedAction : BuiltInGestureAction.None;
            GestureBindingCards.Add(new GestureBindingCardViewModel(
                pattern,
                DirectionText(pattern),
                GestureName(pattern),
                action,
                GestureActionOptions,
                ApplyGestureBindingAsync));
        }
    }

    private async Task ApplyGestureBindingAsync(GestureBindingCardViewModel card)
    {
        var bindings = GestureBindingCards.ToDictionary(item => item.Pattern, item => item.SelectedAction, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(bindings.ToDictionary(
            pair => pair.Key,
            pair => new CustomBindingDto(pair.Value, ShortcutText(pair.Value), pair.Value != BuiltInGestureAction.None),
            StringComparer.Ordinal));
        _gesturePresetProvider.UpdateCustomBindings(bindings);
        _selectedGesturePreset = GesturePreset.Custom;
        UpdateGestureSettingsSnapshot();
        OnPropertyChanged(nameof(SelectedGesturePresetOption));
        await _settingsService.SetAsync(SettingKeys.GestureCustomBindingsJson, json, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.GesturePreset, GesturePreset.Custom, CancellationToken.None);
    }

    private async Task AddCustomGestureBindingAsync()
    {
        var pattern = NormalizeGesturePattern(NewGesturePattern);
        if (!IsValidGesturePattern(pattern))
        {
            return;
        }

        var existing = GestureBindingCards.FirstOrDefault(card => string.Equals(card.Pattern, pattern, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.SelectedAction = NewGestureAction;
            NewGesturePattern = "";
            return;
        }

        var card = new GestureBindingCardViewModel(
            pattern,
            DirectionText(pattern),
            GestureName(pattern),
            NewGestureAction,
            GestureActionOptions,
            ApplyGestureBindingAsync);
        GestureBindingCards.Add(card);
        NewGesturePattern = "";
        await ApplyGestureBindingAsync(card);
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

    public sealed record RetentionOption(string Label, int Days);

    public sealed record GestureStrokeColorOption(string Name, string Color)
    {
        public string DisplayName => $"{Name}  {Color}";
    }

    public sealed record GestureTriggerModeViewModel(string Name, string Status, bool IsEnabled);

    public IReadOnlyList<BuiltInGestureAction> GestureActionOptions { get; } =
    [
        BuiltInGestureAction.None,
        BuiltInGestureAction.Copy,
        BuiltInGestureAction.Paste,
        BuiltInGestureAction.Cut,
        BuiltInGestureAction.SelectAll,
        BuiltInGestureAction.Undo,
        BuiltInGestureAction.Redo,
        BuiltInGestureAction.Enter,
        BuiltInGestureAction.Escape,
        BuiltInGestureAction.Delete,
        BuiltInGestureAction.Backspace,
        BuiltInGestureAction.OpenClipboardOverlay,
        BuiltInGestureAction.PasteLatestClipboardItem,
        BuiltInGestureAction.SendAltLeft,
        BuiltInGestureAction.SendAltRight,
        BuiltInGestureAction.PasteAndEnter,
        BuiltInGestureAction.NewTab,
        BuiltInGestureAction.ReopenClosedTab,
        BuiltInGestureAction.Refresh,
        BuiltInGestureAction.CloseTab,
        BuiltInGestureAction.StartMenu,
        BuiltInGestureAction.ShowDesktop,
        BuiltInGestureAction.SwitchApp,
        BuiltInGestureAction.TaskSwitcher,
        BuiltInGestureAction.PlayPause,
        BuiltInGestureAction.VolumeUp,
        BuiltInGestureAction.VolumeDown,
        BuiltInGestureAction.Mute,
        BuiltInGestureAction.PreviousTrack,
        BuiltInGestureAction.NextTrack,
        BuiltInGestureAction.TaskManager,
        BuiltInGestureAction.SystemSettings,
        BuiltInGestureAction.Sleep,
        BuiltInGestureAction.ZoomIn,
        BuiltInGestureAction.ZoomOut,
        BuiltInGestureAction.ResetZoom,
        BuiltInGestureAction.Home,
        BuiltInGestureAction.End,
        BuiltInGestureAction.PageUp,
        BuiltInGestureAction.PageDown,
        BuiltInGestureAction.Screenshot,
        BuiltInGestureAction.NextVirtualDesktop,
        BuiltInGestureAction.PreviousVirtualDesktop,
        BuiltInGestureAction.FullScreen,
        BuiltInGestureAction.PinWindow
    ];

    private static readonly string[] GesturePatterns = ["U", "D", "UD", "DU", "L", "R", "LR", "RL"];

    private static string NormalizeGesturePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "";
        }

        return new string(pattern.Trim().ToUpperInvariant().Where(ch => ch is 'U' or 'D' or 'L' or 'R').ToArray());
    }

    private static bool IsValidGesturePattern(string pattern)
    {
        return pattern.Length is >= 1 and <= 8 && pattern.All(ch => ch is 'U' or 'D' or 'L' or 'R');
    }

    private static string DirectionText(string pattern) => pattern
        .Replace("U", "↑", StringComparison.Ordinal)
        .Replace("D", "↓", StringComparison.Ordinal)
        .Replace("L", "←", StringComparison.Ordinal)
        .Replace("R", "→", StringComparison.Ordinal);

    private static string GestureName(string pattern) => pattern switch
    {
        "U" => "上划",
        "D" => "下划",
        "UD" => "上下划",
        "DU" => "下上划",
        "L" => "左划",
        "R" => "右划",
        "LR" => "左右划",
        "RL" => "右左划",
        _ => pattern
    };

    private static string ShortcutText(BuiltInGestureAction action) => action switch
    {
        BuiltInGestureAction.Copy => "Ctrl+C",
        BuiltInGestureAction.Paste => "Ctrl+V",
        BuiltInGestureAction.Cut => "Ctrl+X",
        BuiltInGestureAction.SelectAll => "Ctrl+A",
        BuiltInGestureAction.Undo => "Ctrl+Z",
        BuiltInGestureAction.Redo => "Ctrl+Y",
        BuiltInGestureAction.Enter => "Enter",
        BuiltInGestureAction.Escape => "Esc",
        BuiltInGestureAction.Delete => "Delete",
        BuiltInGestureAction.Backspace => "Backspace",
        BuiltInGestureAction.SendAltLeft => "Alt+Left",
        BuiltInGestureAction.SendAltRight => "Alt+Right",
        BuiltInGestureAction.OpenClipboardOverlay => "ClipboardOverlay",
        BuiltInGestureAction.PasteLatestClipboardItem => "PasteLatest",
        BuiltInGestureAction.PasteAndEnter => "Ctrl+V Enter",
        BuiltInGestureAction.NewTab => "Ctrl+T",
        BuiltInGestureAction.ReopenClosedTab => "Ctrl+Shift+T",
        BuiltInGestureAction.Refresh => "F5",
        BuiltInGestureAction.CloseTab => "Ctrl+W",
        BuiltInGestureAction.StartMenu => "Win",
        BuiltInGestureAction.ShowDesktop => "Win+D",
        BuiltInGestureAction.SwitchApp => "Alt+Tab",
        BuiltInGestureAction.TaskSwitcher => "Ctrl+Alt+Tab",
        BuiltInGestureAction.PlayPause => "Media Play/Pause",
        BuiltInGestureAction.VolumeUp => "Volume+",
        BuiltInGestureAction.VolumeDown => "Volume-",
        BuiltInGestureAction.Mute => "Mute",
        BuiltInGestureAction.PreviousTrack => "Previous Track",
        BuiltInGestureAction.NextTrack => "Next Track",
        BuiltInGestureAction.TaskManager => "taskmgr",
        BuiltInGestureAction.SystemSettings => "Win+I",
        BuiltInGestureAction.Sleep => "Sleep",
        BuiltInGestureAction.ZoomIn => "Ctrl+=",
        BuiltInGestureAction.ZoomOut => "Ctrl+-",
        BuiltInGestureAction.ResetZoom => "Ctrl+0",
        BuiltInGestureAction.Home => "Home",
        BuiltInGestureAction.End => "End",
        BuiltInGestureAction.PageUp => "PageUp",
        BuiltInGestureAction.PageDown => "PageDown",
        BuiltInGestureAction.Screenshot => "Win+Shift+S",
        BuiltInGestureAction.NextVirtualDesktop => "Ctrl+Win+Right",
        BuiltInGestureAction.PreviousVirtualDesktop => "Ctrl+Win+Left",
        BuiltInGestureAction.FullScreen => "F11",
        BuiltInGestureAction.PinWindow => "Reserved",
        _ => ""
    };

    private sealed record CustomBindingDto(BuiltInGestureAction Action, string Shortcut, bool IsEnabled);
}
