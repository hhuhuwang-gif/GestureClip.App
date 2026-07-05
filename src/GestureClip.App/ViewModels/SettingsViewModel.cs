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
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Workstation;
using GestureClip.Features.Gestures;
using GestureClip.Features.Workstation;
using GestureClip.Infrastructure.Paths;
using System.Windows.Data;
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
    private readonly IWorkerLevelService _workerLevelService;
    private bool _clipboardCaptureEnabled;
    private bool _gestureEnabled;
    private bool _gestureShowOverlay;
    private bool _gestureDebugEnabled;
    private bool _gestureCloseWindowEnabled;
    private bool _gestureRightButtonEnabled;
    private bool _gestureLeftButtonEnabled;
    private bool _gestureMiddleButtonEnabled;
    private bool _gestureXButton1Enabled;
    private bool _gestureXButton2Enabled;
    private bool _edgeTriggerEnabled;
    private BuiltInGestureAction _edgeTriggerTopLeftAction;
    private BuiltInGestureAction _edgeTriggerTopRightAction;
    private BuiltInGestureAction _edgeTriggerBottomRightAction;
    private BuiltInGestureAction _edgeTriggerBottomLeftAction;
    private bool _edgeTriggerLeftEdgeLeftButtonEnabled;
    private bool _edgeTriggerLeftEdgeMiddleButtonEnabled;
    private bool _edgeTriggerLeftEdgeXButton1Enabled;
    private bool _edgeTriggerLeftEdgeXButton2Enabled;
    private bool _edgeTriggerTopRightWheelEnabled;
    private BuiltInGestureAction _edgeTriggerLeftEdgeLeftButtonAction;
    private BuiltInGestureAction _edgeTriggerLeftEdgeMiddleButtonAction;
    private BuiltInGestureAction _edgeTriggerLeftEdgeXButton1Action;
    private BuiltInGestureAction _edgeTriggerLeftEdgeXButton2Action;
    private BuiltInGestureAction _edgeTriggerTopRightWheelAction;
    private int _edgeTriggerHotZoneSize;
    private int _edgeTriggerDwellMs;
    private int _edgeTriggerCooldownMs;
    private int _edgeTriggerSlideThreshold;
    private bool _edgeTriggerSlideLeftEnabled;
    private bool _edgeTriggerSlideRightEnabled;
    private bool _edgeTriggerSlideTopEnabled;
    private bool _edgeTriggerSlideBottomEnabled;
    private BuiltInGestureAction _edgeTriggerSlideLeftAction;
    private BuiltInGestureAction _edgeTriggerSlideRightAction;
    private BuiltInGestureAction _edgeTriggerSlideTopAction;
    private BuiltInGestureAction _edgeTriggerSlideBottomAction;
    private GesturePreset _selectedGesturePreset;
    private int _gestureTriggerThreshold;
    private GestureDiagnosticsSnapshot _gestureDiagnostics;
    private DiagnosticsSnapshot? _diagnostics;
    private string _newBlacklistProcessName = "";
    private bool _startWithWindows;
    private int _clipboardItemCount;
    private int _clipboardMaxItems;
    private int _clipboardRetentionDays;
    private string _openClipboardHotkeyText;
    private string _gestureStrokeColor;
    private bool _workstationEnabled;
    private decimal _workstationMonthlySalary;
    private string _workstationWorkStartTime = "09:00";
    private string _workstationWorkEndTime = "18:00";
    private string _workstationLunchStartTime = "12:00";
    private string _workstationLunchEndTime = "13:00";
    private string _workstationWorkdays = "1,2,3,4,5";
    private int _workstationPayday;
    private bool _workstationShowFishingValue;
    private bool _workstationShowOffWorkCountdown;
    private bool _workstationDailyReportEnabled;
    private string _workstationCopywritingStyle = "打工人模式";
    private bool _workstationEnableOverworkReminder;
    private int _workstationOverworkReminderIntervalMinutes;
    private double _workstationOverworkHighRiskAfterHours;
    private bool _workstationEnableHudTimeColor;
    private bool _workstationEnableStrongOverworkWarning;
    private bool _workstationOverworkReminderCanSnooze;
    private int _workstationOverworkSnoozeMinutes;
    private string _newGesturePattern = "";
    private BuiltInGestureAction _newGestureAction = BuiltInGestureAction.Copy;
    private string _recordGestureStatusText = "按住左键在方框里画一次。";
    private string _workerLevelText = "Lv.1 初入工位";
    private string _workerXpText = "XP 0 / 50";
    private bool _workerLevelShowLevelUpPopup;
    private bool _workerLevelShowLevelInHud;
    private bool _hudFunTextEnabled;
    private bool _hudStatusLevelEnabled;
    private GestureBindingCardViewModel? _selectedGestureBindingCard;
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
        IEdgeTriggerService edgeTriggerService,
        IWorkerLevelService workerLevelService)
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
        _workerLevelService = workerLevelService;
        GestureActionOptionsView = CollectionViewSource.GetDefaultView(GestureActionOptions);
        GestureActionOptionsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GestureActionOptionViewModel.Category)));
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
        _gestureRightButtonEnabled = _settingsService.Get(SettingKeys.GestureTriggerRightButtonEnabled, true);
        _gestureLeftButtonEnabled = _settingsService.Get(SettingKeys.GestureTriggerLeftButtonEnabled, false);
        _gestureMiddleButtonEnabled = _settingsService.Get(SettingKeys.GestureTriggerMiddleButtonEnabled, false);
        _gestureXButton1Enabled = _settingsService.Get(SettingKeys.GestureTriggerXButton1Enabled, false);
        _gestureXButton2Enabled = _settingsService.Get(SettingKeys.GestureTriggerXButton2Enabled, false);
        _edgeTriggerEnabled = _settingsService.Get(SettingKeys.EdgeTriggerEnabled, false);
        _edgeTriggerTopLeftAction = _settingsService.Get(SettingKeys.EdgeTriggerTopLeftAction, BuiltInGestureAction.StartMenu);
        _edgeTriggerTopRightAction = _settingsService.Get(SettingKeys.EdgeTriggerTopRightAction, BuiltInGestureAction.TaskSwitcher);
        _edgeTriggerBottomRightAction = _settingsService.Get(SettingKeys.EdgeTriggerBottomRightAction, BuiltInGestureAction.ShowDesktop);
        _edgeTriggerBottomLeftAction = _settingsService.Get(SettingKeys.EdgeTriggerBottomLeftAction, BuiltInGestureAction.SwitchApp);
        _edgeTriggerLeftEdgeLeftButtonEnabled = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled, false);
        _edgeTriggerLeftEdgeMiddleButtonEnabled = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled, false);
        _edgeTriggerLeftEdgeXButton1Enabled = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled, false);
        _edgeTriggerLeftEdgeXButton2Enabled = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled, false);
        _edgeTriggerTopRightWheelEnabled = _settingsService.Get(SettingKeys.EdgeTriggerTopRightWheelEnabled, false);
        _edgeTriggerLeftEdgeLeftButtonAction = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction, BuiltInGestureAction.StartMenu);
        _edgeTriggerLeftEdgeMiddleButtonAction = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction, BuiltInGestureAction.ShowDesktop);
        _edgeTriggerLeftEdgeXButton1Action = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton1Action, BuiltInGestureAction.SwitchApp);
        _edgeTriggerLeftEdgeXButton2Action = _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton2Action, BuiltInGestureAction.TaskSwitcher);
        _edgeTriggerTopRightWheelAction = _settingsService.Get(SettingKeys.EdgeTriggerTopRightWheelAction, BuiltInGestureAction.TaskSwitcher);
        _edgeTriggerHotZoneSize = _settingsService.Get(SettingKeys.EdgeTriggerHotZoneSize, 8);
        _edgeTriggerDwellMs = _settingsService.Get(SettingKeys.EdgeTriggerDwellMs, 160);
        _edgeTriggerCooldownMs = _settingsService.Get(SettingKeys.EdgeTriggerCooldownMs, 450);
        _edgeTriggerSlideThreshold = _settingsService.Get(SettingKeys.EdgeTriggerSlideThreshold, 56);
        _edgeTriggerSlideLeftEnabled = _settingsService.Get(SettingKeys.EdgeTriggerSlideLeftEnabled, false);
        _edgeTriggerSlideRightEnabled = _settingsService.Get(SettingKeys.EdgeTriggerSlideRightEnabled, false);
        _edgeTriggerSlideTopEnabled = _settingsService.Get(SettingKeys.EdgeTriggerSlideTopEnabled, false);
        _edgeTriggerSlideBottomEnabled = _settingsService.Get(SettingKeys.EdgeTriggerSlideBottomEnabled, false);
        _edgeTriggerSlideLeftAction = _settingsService.Get(SettingKeys.EdgeTriggerSlideLeftAction, BuiltInGestureAction.SwitchApp);
        _edgeTriggerSlideRightAction = _settingsService.Get(SettingKeys.EdgeTriggerSlideRightAction, BuiltInGestureAction.TaskSwitcher);
        _edgeTriggerSlideTopAction = _settingsService.Get(SettingKeys.EdgeTriggerSlideTopAction, BuiltInGestureAction.StartMenu);
        _edgeTriggerSlideBottomAction = _settingsService.Get(SettingKeys.EdgeTriggerSlideBottomAction, BuiltInGestureAction.PasteAndEnter);
        _selectedGesturePreset = _settingsService.Get(SettingKeys.GesturePreset, GesturePreset.EditEnhanced);
        _gestureTriggerThreshold = _settingsService.Get(SettingKeys.GestureTriggerThreshold, 20);
        _clipboardMaxItems = _settingsService.Get(SettingKeys.ClipboardMaxItems, 1000);
        _clipboardRetentionDays = _settingsService.Get(SettingKeys.ClipboardRetentionDays, 30);
        _workstationEnabled = _settingsService.Get(SettingKeys.WorkstationEnabled, true);
        _workstationMonthlySalary = _settingsService.Get(SettingKeys.WorkstationMonthlySalary, 0m);
        _workstationWorkStartTime = _settingsService.Get(SettingKeys.WorkstationWorkStartTime, "09:00");
        _workstationWorkEndTime = _settingsService.Get(SettingKeys.WorkstationWorkEndTime, "18:00");
        _workstationLunchStartTime = _settingsService.Get(SettingKeys.WorkstationLunchStartTime, "12:00");
        _workstationLunchEndTime = _settingsService.Get(SettingKeys.WorkstationLunchEndTime, "13:00");
        _workstationWorkdays = _settingsService.Get(SettingKeys.WorkstationWorkdays, "1,2,3,4,5");
        _workstationPayday = _settingsService.Get(SettingKeys.WorkstationPayday, 15);
        _workstationShowFishingValue = _settingsService.Get(SettingKeys.WorkstationShowFishingValue, true);
        _workstationShowOffWorkCountdown = _settingsService.Get(SettingKeys.WorkstationShowOffWorkCountdown, true);
        _workstationDailyReportEnabled = _settingsService.Get(SettingKeys.WorkstationDailyReportEnabled, false);
        _workstationCopywritingStyle = _settingsService.Get(SettingKeys.WorkstationCopywritingStyle, "打工人模式");
        _workstationEnableOverworkReminder = _settingsService.Get(SettingKeys.WorkstationEnableOverworkReminder, true);
        _workstationOverworkReminderIntervalMinutes = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderIntervalMinutes, 60), 30, 180);
        _workstationOverworkHighRiskAfterHours = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkHighRiskAfterHours, 8d), 6d, 12d);
        _workstationEnableHudTimeColor = _settingsService.Get(SettingKeys.WorkstationEnableHudTimeColor, true);
        _workstationEnableStrongOverworkWarning = _settingsService.Get(SettingKeys.WorkstationEnableStrongOverworkWarning, false);
        _workstationOverworkReminderCanSnooze = _settingsService.Get(SettingKeys.WorkstationOverworkReminderCanSnooze, true);
        _workstationOverworkSnoozeMinutes = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkSnoozeMinutes, 15), 5, 60);
        _workerLevelShowLevelUpPopup = _settingsService.Get(SettingKeys.WorkerLevelShowLevelUpPopup, true);
        _workerLevelShowLevelInHud = _settingsService.Get(SettingKeys.WorkerLevelShowLevelInHud, true);
        _hudFunTextEnabled = _settingsService.Get(SettingKeys.HudFunTextEnabled, true);
        _hudStatusLevelEnabled = _settingsService.Get(SettingKeys.HudStatusLevelEnabled, true);
        _openClipboardHotkeyText = HotkeyDefinition.ParseOrDefault(_settingsService.Get(
            SettingKeys.HotkeyOpenClipboardOverlayKey,
            HotkeyDefinition.DefaultOpenClipboardOverlay)).DisplayText;
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
        DeleteSelectedGestureBindingCommand = new AsyncRelayCommand(_ => DeleteSelectedGestureBindingAsync());
        SetNewGesturePatternCommand = new RelayCommand(SetNewGesturePattern);
        SetNewGestureActionCommand = new RelayCommand(SetNewGestureAction);
        SetNewGestureTemplateCommand = new RelayCommand(SetNewGestureTemplate);
        SetSelectedGestureActionCommand = new RelayCommand(SetSelectedGestureAction);
        AppendGestureDirectionCommand = new RelayCommand(AppendGestureDirection);
        RemoveLastGestureDirectionCommand = new RelayCommand(_ => RemoveLastGestureDirection());
        ClearNewGesturePatternCommand = new RelayCommand(_ => NewGesturePattern = "");
        ApplyBrowserEdgePresetCommand = new AsyncRelayCommand(_ => ApplyBrowserEdgePresetAsync());
        ApplySystemEdgePresetCommand = new AsyncRelayCommand(_ => ApplySystemEdgePresetAsync());
        ApplyClipboardEdgePresetCommand = new AsyncRelayCommand(_ => ApplyClipboardEdgePresetAsync());
        ApplyWorkstationTemplateCommand = new RelayCommand(ApplyWorkstationTemplate);
        OpenLogDirectoryCommand = new RelayCommand(_ => OpenDirectory(LogDirectory));
        OpenDataDirectoryCommand = new RelayCommand(_ => OpenDirectory(AppDataPath));
        _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _diagnosticsTimer.Tick += (_, _) => RefreshGestureDiagnostics();
        _diagnosticsTimer.Start();
        _ = LoadBlacklistAsync();
        _ = RefreshDiagnosticsAsync();
        _ = RefreshClipboardStatsAsync();
        RefreshGestureBindingCards();
        _ = RefreshWorkerLevelAsync();
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

    public string OpenClipboardHotkeyText
    {
        get => _openClipboardHotkeyText;
        set
        {
            var hotkey = HotkeyDefinition.ParseOrDefault(value);
            if (_openClipboardHotkeyText == hotkey.DisplayText)
            {
                return;
            }

            _openClipboardHotkeyText = hotkey.DisplayText;
            OnPropertyChanged();
            _ = ApplyOpenClipboardHotkeyAsync(hotkey.DisplayText);
        }
    }



    public string WorkerLevelText
    {
        get => _workerLevelText;
        private set
        {
            if (_workerLevelText == value)
            {
                return;
            }

            _workerLevelText = value;
            OnPropertyChanged();
        }
    }

    public string WorkerXpText
    {
        get => _workerXpText;
        private set
        {
            if (_workerXpText == value)
            {
                return;
            }

            _workerXpText = value;
            OnPropertyChanged();
        }
    }

    public bool WorkerLevelShowLevelUpPopup
    {
        get => _workerLevelShowLevelUpPopup;
        set
        {
            if (_workerLevelShowLevelUpPopup == value)
            {
                return;
            }

            _workerLevelShowLevelUpPopup = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkerLevelShowLevelUpPopup, value, CancellationToken.None);
        }
    }

    public bool WorkerLevelShowLevelInHud
    {
        get => _workerLevelShowLevelInHud;
        set
        {
            if (_workerLevelShowLevelInHud == value)
            {
                return;
            }

            _workerLevelShowLevelInHud = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkerLevelShowLevelInHud, value, CancellationToken.None);
        }
    }

    public bool HudFunTextEnabled
    {
        get => _hudFunTextEnabled;
        set
        {
            if (_hudFunTextEnabled == value)
            {
                return;
            }

            _hudFunTextEnabled = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.HudFunTextEnabled, value, CancellationToken.None);
        }
    }

    public bool HudStatusLevelEnabled
    {
        get => _hudStatusLevelEnabled;
        set
        {
            if (_hudStatusLevelEnabled == value)
            {
                return;
            }

            _hudStatusLevelEnabled = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.HudStatusLevelEnabled, value, CancellationToken.None);
        }
    }

    public ObservableCollection<AppBlacklistItemViewModel> AppBlacklistItems { get; } = [];

    public ObservableCollection<GestureBindingCardViewModel> GestureBindingCards { get; } = [];

    public ObservableCollection<GestureBindingCardViewModel> PrimaryGestureBindingCards { get; } = [];

    public ObservableCollection<GestureBindingCardViewModel> AdvancedGestureBindingCards { get; } = [];

    public GestureBindingCardViewModel? SelectedGestureBindingCard
    {
        get => _selectedGestureBindingCard;
        set
        {
            if (ReferenceEquals(_selectedGestureBindingCard, value))
            {
                return;
            }

            _selectedGestureBindingCard = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedGestureBinding));
            OnPropertyChanged(nameof(SelectedGestureBindingPattern));
            OnPropertyChanged(nameof(SelectedGestureBindingDirectionText));
            OnPropertyChanged(nameof(SelectedGestureBindingActionName));
            OnPropertyChanged(nameof(SelectedGestureBindingShortcutText));
            OnPropertyChanged(nameof(SelectedGestureBindingEmptyText));
        }
    }

    public bool HasSelectedGestureBinding => SelectedGestureBindingCard is not null;

    public string SelectedGestureBindingPattern => SelectedGestureBindingCard?.Pattern ?? "-";

    public string SelectedGestureBindingDirectionText => SelectedGestureBindingCard?.DirectionText ?? "先从左侧选择一个手势";

    public string SelectedGestureBindingActionName => SelectedGestureBindingCard?.ActionName ?? "-";

    public string SelectedGestureBindingShortcutText => SelectedGestureBindingCard?.ShortcutText ?? "";

    public string SelectedGestureBindingEmptyText => SelectedGestureBindingCard is null
        ? "请选择左侧手势进行编辑"
        : SelectedGestureBindingCard.SelectedAction == BuiltInGestureAction.None
            ? "该手势尚未绑定动作，选择一个动作进行绑定。"
            : "修改后会自动保存。";

    public IReadOnlyList<GestureTriggerModeViewModel> GestureTriggerModes { get; } =
    [
        new("鼠标右键", "默认推荐", true),
        new("鼠标中键", "可选", true),
        new("鼠标侧键 1", "可选", true),
        new("鼠标侧键 2", "可选", true),
        new("屏幕边缘 + 鼠标滑动", "可选", true)
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

    public ICommand DeleteSelectedGestureBindingCommand { get; }

    public ICommand SetNewGesturePatternCommand { get; }

    public ICommand SetNewGestureActionCommand { get; }

    public ICommand SetNewGestureTemplateCommand { get; }

    public ICommand SetSelectedGestureActionCommand { get; }

    public ICommand AppendGestureDirectionCommand { get; }

    public ICommand RemoveLastGestureDirectionCommand { get; }

    public ICommand ClearNewGesturePatternCommand { get; }

    public ICommand ApplyBrowserEdgePresetCommand { get; }

    public ICommand ApplySystemEdgePresetCommand { get; }

    public ICommand ApplyClipboardEdgePresetCommand { get; }

    public ICommand ApplyWorkstationTemplateCommand { get; }

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

    public bool WorkstationEnabled
    {
        get => _workstationEnabled;
        set
        {
            if (_workstationEnabled == value)
            {
                return;
            }

            _workstationEnabled = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationEnabled, value, CancellationToken.None);
        }
    }

    public decimal WorkstationMonthlySalary
    {
        get => _workstationMonthlySalary;
        set
        {
            if (_workstationMonthlySalary == value)
            {
                return;
            }

            _workstationMonthlySalary = Math.Max(0m, value);
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationMonthlySalary, _workstationMonthlySalary, CancellationToken.None);
        }
    }

    public string WorkstationWorkStartTime
    {
        get => _workstationWorkStartTime;
        set => SetWorkstationTimeSetting(ref _workstationWorkStartTime, value, SettingKeys.WorkstationWorkStartTime);
    }

    public string WorkstationWorkEndTime
    {
        get => _workstationWorkEndTime;
        set => SetWorkstationTimeSetting(ref _workstationWorkEndTime, value, SettingKeys.WorkstationWorkEndTime);
    }

    public string WorkstationLunchStartTime
    {
        get => _workstationLunchStartTime;
        set => SetWorkstationTimeSetting(ref _workstationLunchStartTime, value, SettingKeys.WorkstationLunchStartTime);
    }

    public string WorkstationLunchEndTime
    {
        get => _workstationLunchEndTime;
        set => SetWorkstationTimeSetting(ref _workstationLunchEndTime, value, SettingKeys.WorkstationLunchEndTime);
    }

    public string WorkstationWorkdays
    {
        get => _workstationWorkdays;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "1,2,3,4,5" : value.Trim();
            if (_workstationWorkdays == normalized)
            {
                return;
            }

            _workstationWorkdays = normalized;
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationWorkdays, normalized, CancellationToken.None);
        }
    }

    public int WorkstationPayday
    {
        get => _workstationPayday;
        set
        {
            var normalized = Math.Clamp(value, 1, 28);
            if (_workstationPayday == normalized)
            {
                return;
            }

            _workstationPayday = normalized;
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationPayday, normalized, CancellationToken.None);
        }
    }

    public IReadOnlyList<WorkstationTemplateOption> WorkstationTemplateOptions { get; } =
    [
        new("标准双休 09:00-18:00", "09:00", "18:00", "12:00", "13:00", "1,2,3,4,5"),
        new("996 09:00-21:00", "09:00", "21:00", "12:00", "13:00", "1,2,3,4,5,6"),
        new("单休 09:00-18:30", "09:00", "18:30", "12:00", "13:30", "1,2,3,4,5,6"),
        new("弹性 10:00-19:00", "10:00", "19:00", "12:30", "13:30", "1,2,3,4,5")
    ];

    public string WorkstationPreviewStatusText => $"当前状态：{GetWorkstationStatusText(DateTime.Now)}";

    public string WorkstationPreviewTodayEarnedText
    {
        get
        {
            var earned = EstimateTodayEarned(DateTime.Now);
            return $"今天已赚：¥{earned:0.00}";
        }
    }

    public string WorkstationPreviewOffWorkText
    {
        get
        {
            var now = DateTime.Now;
            if (!TryParseTime(WorkstationWorkEndTime, out var end))
            {
                return "距离下班：时间配置有误";
            }

            var offWorkAt = now.Date.Add(end);
            if (now >= offWorkAt)
            {
                return "距离下班：已下班，今天赢了";
            }

            var remaining = offWorkAt - now;
            return $"距离下班：{(int)remaining.TotalHours:00}:{remaining.Minutes:00}";
        }
    }

    public string WorkstationPreviewPaydayText
    {
        get
        {
            var now = DateTime.Today;
            var payday = Math.Clamp(WorkstationPayday, 1, 28);
            var payDate = new DateTime(now.Year, now.Month, payday);
            if (now > payDate)
            {
                payDate = payDate.AddMonths(1);
            }

            var days = Math.Max(0, (payDate - now).Days);
            return days == 0 ? "距离发薪：今天发钱" : $"距离发薪：{days} 天";
        }
    }

    public string WorkstationPreviewSummaryText =>
        $"{WorkstationPreviewStatusText} · {WorkstationPreviewTodayEarnedText} · {WorkstationPreviewOffWorkText}";

    public IReadOnlyList<string> WorkstationCopywritingStyleOptions { get; } =
    [
        "正常模式",
        "打工人模式",
        "抽象模式"
    ];

    public string WorkstationCopywritingStyle
    {
        get => _workstationCopywritingStyle;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "打工人模式" : value.Trim();
            if (_workstationCopywritingStyle == normalized)
            {
                return;
            }

            _workstationCopywritingStyle = normalized;
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationCopywritingStyle, normalized, CancellationToken.None);
        }
    }

    public bool WorkstationShowFishingValue
    {
        get => _workstationShowFishingValue;
        set
        {
            if (_workstationShowFishingValue == value)
            {
                return;
            }

            _workstationShowFishingValue = value;
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationShowFishingValue, value, CancellationToken.None);
        }
    }

    public bool WorkstationShowOffWorkCountdown
    {
        get => _workstationShowOffWorkCountdown;
        set
        {
            if (_workstationShowOffWorkCountdown == value)
            {
                return;
            }

            _workstationShowOffWorkCountdown = value;
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationShowOffWorkCountdown, value, CancellationToken.None);
        }
    }

    public bool WorkstationDailyReportEnabled
    {
        get => _workstationDailyReportEnabled;
        set
        {
            if (_workstationDailyReportEnabled == value)
            {
                return;
            }

            _workstationDailyReportEnabled = value;
            OnPropertyChanged();
            NotifyWorkstationPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationDailyReportEnabled, value, CancellationToken.None);
        }
    }

    public bool WorkstationEnableOverworkReminder
    {
        get => _workstationEnableOverworkReminder;
        set
        {
            if (_workstationEnableOverworkReminder == value)
            {
                return;
            }

            _workstationEnableOverworkReminder = value;
            OnPropertyChanged();
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, value, CancellationToken.None);
        }
    }

    public int WorkstationOverworkReminderIntervalMinutes
    {
        get => _workstationOverworkReminderIntervalMinutes;
        set
        {
            var normalized = Math.Clamp(value, 30, 180);
            if (_workstationOverworkReminderIntervalMinutes == normalized)
            {
                return;
            }

            _workstationOverworkReminderIntervalMinutes = normalized;
            OnPropertyChanged();
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationOverworkReminderIntervalMinutes, normalized, CancellationToken.None);
        }
    }

    public double WorkstationOverworkHighRiskAfterHours
    {
        get => _workstationOverworkHighRiskAfterHours;
        set
        {
            var normalized = Math.Clamp(value, 6d, 12d);
            if (Math.Abs(_workstationOverworkHighRiskAfterHours - normalized) < 0.001)
            {
                return;
            }

            _workstationOverworkHighRiskAfterHours = normalized;
            OnPropertyChanged();
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationOverworkHighRiskAfterHours, normalized, CancellationToken.None);
        }
    }

    public bool WorkstationEnableHudTimeColor
    {
        get => _workstationEnableHudTimeColor;
        set
        {
            if (_workstationEnableHudTimeColor == value)
            {
                return;
            }

            _workstationEnableHudTimeColor = value;
            OnPropertyChanged();
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationEnableHudTimeColor, value, CancellationToken.None);
        }
    }

    public bool WorkstationEnableStrongOverworkWarning
    {
        get => _workstationEnableStrongOverworkWarning;
        set
        {
            if (_workstationEnableStrongOverworkWarning == value)
            {
                return;
            }

            _workstationEnableStrongOverworkWarning = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationEnableStrongOverworkWarning, value, CancellationToken.None);
        }
    }

    public bool WorkstationOverworkReminderCanSnooze
    {
        get => _workstationOverworkReminderCanSnooze;
        set
        {
            if (_workstationOverworkReminderCanSnooze == value)
            {
                return;
            }

            _workstationOverworkReminderCanSnooze = value;
            OnPropertyChanged();
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationOverworkReminderCanSnooze, value, CancellationToken.None);
        }
    }

    public int WorkstationOverworkSnoozeMinutes
    {
        get => _workstationOverworkSnoozeMinutes;
        set
        {
            var normalized = Math.Clamp(value, 5, 60);
            if (_workstationOverworkSnoozeMinutes == normalized)
            {
                return;
            }

            _workstationOverworkSnoozeMinutes = normalized;
            OnPropertyChanged();
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationOverworkSnoozeMinutes, normalized, CancellationToken.None);
        }
    }

    public string OverworkPreviewStageText
    {
        get
        {
            var snapshot = GetOverworkPreviewSnapshot(DateTime.Now);
            return $"当前阶段：{GetStageDisplayName(snapshot.Stage)}";
        }
    }

    public string OverworkPreviewHudColorText
    {
        get
        {
            var snapshot = GetOverworkPreviewSnapshot(DateTime.Now);
            var theme = WorkstationEnableHudTimeColor
                ? snapshot.Theme
                : WorkTimeStageThemeProvider.GetTheme(WorkTimeStage.OffWork);
            return $"HUD 颜色：{theme.FriendlyColorName}";
        }
    }

    public string OverworkPreviewNextReminderText =>
        WorkstationEnableOverworkReminder
            ? $"下次提醒：连续工作约 {WorkstationOverworkReminderIntervalMinutes} 分钟后"
            : "下次提醒：已关闭";

    public string OverworkPreviewWorkedText
    {
        get
        {
            var worked = GetOverworkPreviewSnapshot(DateTime.Now).EffectiveWorkedTime;
            return $"今日连续工作：{(int)worked.TotalHours:00}:{worked.Minutes:00}";
        }
    }

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
            OnPropertyChanged(nameof(ClipboardCaptureStatusText));
            _ = ApplyClipboardCaptureEnabledAsync(value);
        }
    }

    public string ClipboardCaptureStatusText => ClipboardCaptureEnabled ? "已开启" : "已暂停";

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
        _ = RefreshWorkerLevelAsync();
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
            OnPropertyChanged(nameof(GestureStatusText));
            _ = ApplyGestureEnabledAsync(value);
        }
    }

    public string GestureStatusText => GestureEnabled ? "已开启" : "已暂停";

    public string EnabledGestureTriggerSummary
    {
        get
        {
            var names = new List<string>();
            if (GestureRightButtonEnabled)
            {
                names.Add("鼠标右键");
            }

            if (GestureMiddleButtonEnabled)
            {
                names.Add("鼠标中键");
            }

            if (GestureXButton1Enabled)
            {
                names.Add("鼠标侧键 1");
            }

            if (GestureXButton2Enabled)
            {
                names.Add("鼠标侧键 2");
            }

            if (EdgeTriggerEnabled)
            {
                names.Add("屏幕边缘");
            }

            return names.Count == 0
                ? "当前启用：无，请至少开启一种触发方式"
                : $"当前启用：{string.Join(" + ", names)}";
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
            OnPropertyChanged(nameof(EnabledGestureTriggerSummary));
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerMiddleButtonEnabled, value, CancellationToken.None);
        }
    }

    public bool GestureRightButtonEnabled
    {
        get => _gestureRightButtonEnabled;
        set
        {
            if (_gestureRightButtonEnabled == value)
            {
                return;
            }

            _gestureRightButtonEnabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            OnPropertyChanged(nameof(EnabledGestureTriggerSummary));
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerRightButtonEnabled, value, CancellationToken.None);
        }
    }

    public bool GestureLeftButtonEnabled
    {
        get => _gestureLeftButtonEnabled;
        set
        {
            if (_gestureLeftButtonEnabled == value)
            {
                return;
            }

            _gestureLeftButtonEnabled = value;
            UpdateGestureSettingsSnapshot();
            OnPropertyChanged();
            OnPropertyChanged(nameof(EnabledGestureTriggerSummary));
            _ = _settingsService.SetAsync(SettingKeys.GestureTriggerLeftButtonEnabled, value, CancellationToken.None);
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
            OnPropertyChanged(nameof(EnabledGestureTriggerSummary));
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
            OnPropertyChanged(nameof(EnabledGestureTriggerSummary));
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
            OnPropertyChanged(nameof(EnabledGestureTriggerSummary));
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

    public bool EdgeTriggerLeftEdgeLeftButtonEnabled
    {
        get => _edgeTriggerLeftEdgeLeftButtonEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerLeftEdgeLeftButtonEnabled, value, SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled);
    }

    public BuiltInGestureAction EdgeTriggerLeftEdgeLeftButtonAction
    {
        get => _edgeTriggerLeftEdgeLeftButtonAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerLeftEdgeLeftButtonAction, value, SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction);
    }

    public bool EdgeTriggerLeftEdgeMiddleButtonEnabled
    {
        get => _edgeTriggerLeftEdgeMiddleButtonEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerLeftEdgeMiddleButtonEnabled, value, SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled);
    }

    public BuiltInGestureAction EdgeTriggerLeftEdgeMiddleButtonAction
    {
        get => _edgeTriggerLeftEdgeMiddleButtonAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerLeftEdgeMiddleButtonAction, value, SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction);
    }

    public bool EdgeTriggerLeftEdgeXButton1Enabled
    {
        get => _edgeTriggerLeftEdgeXButton1Enabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerLeftEdgeXButton1Enabled, value, SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled);
    }

    public BuiltInGestureAction EdgeTriggerLeftEdgeXButton1Action
    {
        get => _edgeTriggerLeftEdgeXButton1Action;
        set => SetEdgeTriggerAction(ref _edgeTriggerLeftEdgeXButton1Action, value, SettingKeys.EdgeTriggerLeftEdgeXButton1Action);
    }

    public bool EdgeTriggerLeftEdgeXButton2Enabled
    {
        get => _edgeTriggerLeftEdgeXButton2Enabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerLeftEdgeXButton2Enabled, value, SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled);
    }

    public BuiltInGestureAction EdgeTriggerLeftEdgeXButton2Action
    {
        get => _edgeTriggerLeftEdgeXButton2Action;
        set => SetEdgeTriggerAction(ref _edgeTriggerLeftEdgeXButton2Action, value, SettingKeys.EdgeTriggerLeftEdgeXButton2Action);
    }

    public bool EdgeTriggerTopRightWheelEnabled
    {
        get => _edgeTriggerTopRightWheelEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerTopRightWheelEnabled, value, SettingKeys.EdgeTriggerTopRightWheelEnabled);
    }

    public BuiltInGestureAction EdgeTriggerTopRightWheelAction
    {
        get => _edgeTriggerTopRightWheelAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerTopRightWheelAction, value, SettingKeys.EdgeTriggerTopRightWheelAction);
    }

    public int EdgeTriggerHotZoneSize
    {
        get => _edgeTriggerHotZoneSize;
        set => SetEdgeTriggerInt(ref _edgeTriggerHotZoneSize, Math.Clamp(value, 2, 64), SettingKeys.EdgeTriggerHotZoneSize, nameof(EdgeTriggerHotZoneSize));
    }

    public int EdgeTriggerDwellMs
    {
        get => _edgeTriggerDwellMs;
        set => SetEdgeTriggerInt(ref _edgeTriggerDwellMs, Math.Clamp(value, 50, 2000), SettingKeys.EdgeTriggerDwellMs, nameof(EdgeTriggerDwellMs));
    }

    public int EdgeTriggerCooldownMs
    {
        get => _edgeTriggerCooldownMs;
        set => SetEdgeTriggerInt(ref _edgeTriggerCooldownMs, Math.Clamp(value, 150, 5000), SettingKeys.EdgeTriggerCooldownMs, nameof(EdgeTriggerCooldownMs));
    }

    public int EdgeTriggerSlideThreshold
    {
        get => _edgeTriggerSlideThreshold;
        set => SetEdgeTriggerInt(ref _edgeTriggerSlideThreshold, Math.Clamp(value, 24, 400), SettingKeys.EdgeTriggerSlideThreshold, nameof(EdgeTriggerSlideThreshold));
    }

    public bool EdgeTriggerSlideLeftEnabled
    {
        get => _edgeTriggerSlideLeftEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerSlideLeftEnabled, value, SettingKeys.EdgeTriggerSlideLeftEnabled);
    }

    public BuiltInGestureAction EdgeTriggerSlideLeftAction
    {
        get => _edgeTriggerSlideLeftAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerSlideLeftAction, value, SettingKeys.EdgeTriggerSlideLeftAction);
    }

    public bool EdgeTriggerSlideRightEnabled
    {
        get => _edgeTriggerSlideRightEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerSlideRightEnabled, value, SettingKeys.EdgeTriggerSlideRightEnabled);
    }

    public BuiltInGestureAction EdgeTriggerSlideRightAction
    {
        get => _edgeTriggerSlideRightAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerSlideRightAction, value, SettingKeys.EdgeTriggerSlideRightAction);
    }

    public bool EdgeTriggerSlideTopEnabled
    {
        get => _edgeTriggerSlideTopEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerSlideTopEnabled, value, SettingKeys.EdgeTriggerSlideTopEnabled);
    }

    public BuiltInGestureAction EdgeTriggerSlideTopAction
    {
        get => _edgeTriggerSlideTopAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerSlideTopAction, value, SettingKeys.EdgeTriggerSlideTopAction);
    }

    public bool EdgeTriggerSlideBottomEnabled
    {
        get => _edgeTriggerSlideBottomEnabled;
        set => SetEdgeTriggerEnabled(ref _edgeTriggerSlideBottomEnabled, value, SettingKeys.EdgeTriggerSlideBottomEnabled);
    }

    public BuiltInGestureAction EdgeTriggerSlideBottomAction
    {
        get => _edgeTriggerSlideBottomAction;
        set => SetEdgeTriggerAction(ref _edgeTriggerSlideBottomAction, value, SettingKeys.EdgeTriggerSlideBottomAction);
    }

    public string EdgeTriggerLastSource => _edgeTriggerService.Diagnostics.LastSource;

    public string EdgeTriggerLastPosition => _edgeTriggerService.Diagnostics.LastPosition;

    public string EdgeTriggerLastAction => GestureActionText.Name(_edgeTriggerService.Diagnostics.LastAction);

    public string EdgeTriggerLastReason => _edgeTriggerService.Diagnostics.LastReason;

    public string EdgeTriggerLastEventTime => _edgeTriggerService.Diagnostics.LastEventAt is null
        ? "-"
        : _edgeTriggerService.Diagnostics.LastEventAt.Value.ToLocalTime().ToString("HH:mm:ss.fff");

    public string EdgeTriggerCooldownText => _edgeTriggerService.Diagnostics.CooldownUntil is null
        ? "未冷却"
        : $"冷却到 {_edgeTriggerService.Diagnostics.CooldownUntil.Value.ToLocalTime():HH:mm:ss.fff}";

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
            OnPropertyChanged(nameof(NewGestureDirectionPreview));
            OnPropertyChanged(nameof(NewGestureAddButtonText));
        }
    }

    public string NewGestureDirectionPreview => string.IsNullOrEmpty(NewGesturePattern)
        ? "点击方向按钮设计手势"
        : DirectionText(NewGesturePattern);

    public string NewGestureAddButtonText => string.IsNullOrEmpty(NewGesturePattern)
        ? "先选方向"
        : "添加到列表";

    public string RecordGestureStatusText
    {
        get => _recordGestureStatusText;
        private set
        {
            if (_recordGestureStatusText == value)
            {
                return;
            }

            _recordGestureStatusText = value;
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

    private async Task ApplyOpenClipboardHotkeyAsync(string hotkeyText)
    {
        await _settingsService.SetAsync(SettingKeys.HotkeyOpenClipboardOverlayKey, hotkeyText, CancellationToken.None);
        _globalHotkeyService.Stop();
        _globalHotkeyService.Start();
        OnPropertyChanged(nameof(HotkeyStatusText));
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
            SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction => nameof(EdgeTriggerLeftEdgeLeftButtonAction),
            SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction => nameof(EdgeTriggerLeftEdgeMiddleButtonAction),
            SettingKeys.EdgeTriggerLeftEdgeXButton1Action => nameof(EdgeTriggerLeftEdgeXButton1Action),
            SettingKeys.EdgeTriggerLeftEdgeXButton2Action => nameof(EdgeTriggerLeftEdgeXButton2Action),
            SettingKeys.EdgeTriggerTopRightWheelAction => nameof(EdgeTriggerTopRightWheelAction),
            SettingKeys.EdgeTriggerSlideLeftAction => nameof(EdgeTriggerSlideLeftAction),
            SettingKeys.EdgeTriggerSlideRightAction => nameof(EdgeTriggerSlideRightAction),
            SettingKeys.EdgeTriggerSlideTopAction => nameof(EdgeTriggerSlideTopAction),
            SettingKeys.EdgeTriggerSlideBottomAction => nameof(EdgeTriggerSlideBottomAction),
            _ => null
        });
        _ = SaveEdgeTriggerSettingAndRefreshAsync(settingKey, value);
    }

    private void SetEdgeTriggerEnabled(ref bool field, bool value, string settingKey)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(settingKey switch
        {
            SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled => nameof(EdgeTriggerLeftEdgeLeftButtonEnabled),
            SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled => nameof(EdgeTriggerLeftEdgeMiddleButtonEnabled),
            SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled => nameof(EdgeTriggerLeftEdgeXButton1Enabled),
            SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled => nameof(EdgeTriggerLeftEdgeXButton2Enabled),
            SettingKeys.EdgeTriggerTopRightWheelEnabled => nameof(EdgeTriggerTopRightWheelEnabled),
            SettingKeys.EdgeTriggerSlideLeftEnabled => nameof(EdgeTriggerSlideLeftEnabled),
            SettingKeys.EdgeTriggerSlideRightEnabled => nameof(EdgeTriggerSlideRightEnabled),
            SettingKeys.EdgeTriggerSlideTopEnabled => nameof(EdgeTriggerSlideTopEnabled),
            SettingKeys.EdgeTriggerSlideBottomEnabled => nameof(EdgeTriggerSlideBottomEnabled),
            _ => null
        });
        _ = SaveEdgeTriggerSettingAndRefreshAsync(settingKey, value);
    }

    private void SetEdgeTriggerInt(ref int field, int value, string settingKey, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        _ = SaveEdgeTriggerSettingAndRefreshAsync(settingKey, value);
    }

    private async Task SaveEdgeTriggerSettingAndRefreshAsync<T>(string settingKey, T value)
    {
        await _settingsService.SetAsync(settingKey, value, CancellationToken.None);
        _edgeTriggerService.RefreshSettings();
    }

    private async Task ApplyBrowserEdgePresetAsync()
    {
        await ApplyEdgePresetAsync(
            enableLeftMiddle: true,
            leftMiddleAction: BuiltInGestureAction.Refresh,
            enableX1: true,
            x1Action: BuiltInGestureAction.SendAltLeft,
            enableX2: true,
            x2Action: BuiltInGestureAction.SendAltRight,
            enableWheel: true,
            wheelAction: BuiltInGestureAction.TaskSwitcher);
    }

    private async Task ApplySystemEdgePresetAsync()
    {
        await ApplyEdgePresetAsync(
            enableLeftMiddle: true,
            leftMiddleAction: BuiltInGestureAction.ShowDesktop,
            enableX1: true,
            x1Action: BuiltInGestureAction.SwitchApp,
            enableX2: true,
            x2Action: BuiltInGestureAction.TaskSwitcher,
            enableWheel: true,
            wheelAction: BuiltInGestureAction.VolumeUp);
    }

    private async Task ApplyClipboardEdgePresetAsync()
    {
        await ApplyEdgePresetAsync(
            enableLeftMiddle: true,
            leftMiddleAction: BuiltInGestureAction.OpenClipboardOverlay,
            enableX1: true,
            x1Action: BuiltInGestureAction.PasteLatestClipboardItem,
            enableX2: true,
            x2Action: BuiltInGestureAction.Copy,
            enableWheel: false,
            wheelAction: BuiltInGestureAction.TaskSwitcher);
    }

    private async Task ApplyEdgePresetAsync(
        bool enableLeftMiddle,
        BuiltInGestureAction leftMiddleAction,
        bool enableX1,
        BuiltInGestureAction x1Action,
        bool enableX2,
        BuiltInGestureAction x2Action,
        bool enableWheel,
        BuiltInGestureAction wheelAction)
    {
        EdgeTriggerEnabled = true;
        EdgeTriggerLeftEdgeMiddleButtonEnabled = enableLeftMiddle;
        EdgeTriggerLeftEdgeMiddleButtonAction = leftMiddleAction;
        EdgeTriggerLeftEdgeXButton1Enabled = enableX1;
        EdgeTriggerLeftEdgeXButton1Action = x1Action;
        EdgeTriggerLeftEdgeXButton2Enabled = enableX2;
        EdgeTriggerLeftEdgeXButton2Action = x2Action;
        EdgeTriggerTopRightWheelEnabled = enableWheel;
        EdgeTriggerTopRightWheelAction = wheelAction;
        EdgeTriggerSlideLeftEnabled = true;
        EdgeTriggerSlideLeftAction = BuiltInGestureAction.SwitchApp;
        EdgeTriggerSlideRightEnabled = true;
        EdgeTriggerSlideRightAction = BuiltInGestureAction.TaskSwitcher;
        EdgeTriggerSlideBottomEnabled = true;
        EdgeTriggerSlideBottomAction = BuiltInGestureAction.PasteAndEnter;
        await _settingsService.SetAsync(SettingKeys.EdgeTriggerEnabled, true, CancellationToken.None);
    }



    private async Task RefreshWorkerLevelAsync()
    {
        try
        {
            var snapshot = await _workerLevelService.GetSnapshotAsync(CancellationToken.None);
            WorkerLevelText = snapshot.LevelText;
            WorkerXpText = snapshot.XpText;
        }
        catch
        {
            WorkerLevelText = "Lv.1 初入工位";
            WorkerXpText = "XP 0 / 50";
        }
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

    private void SetWorkstationTimeSetting(ref string field, string value, string key, [CallerMemberName] string? propertyName = null)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "00:00" : value.Trim();
        if (field == normalized)
        {
            return;
        }

        field = normalized;
        OnPropertyChanged(propertyName);
        NotifyWorkstationPreviewChanged();
        _ = _settingsService.SetAsync(key, normalized, CancellationToken.None);
    }

    private void ApplyWorkstationTemplate(object? parameter)
    {
        if (parameter is not WorkstationTemplateOption template)
        {
            return;
        }

        WorkstationWorkStartTime = template.WorkStartTime;
        WorkstationWorkEndTime = template.WorkEndTime;
        WorkstationLunchStartTime = template.LunchStartTime;
        WorkstationLunchEndTime = template.LunchEndTime;
        WorkstationWorkdays = template.Workdays;
    }

    private decimal EstimateTodayEarned(DateTime now)
    {
        if (WorkstationMonthlySalary <= 0m ||
            !IsWorkday(now.DayOfWeek) ||
            !TryParseTime(WorkstationWorkStartTime, out var start) ||
            !TryParseTime(WorkstationWorkEndTime, out var end))
        {
            return 0m;
        }

        var startAt = now.Date.Add(start);
        var endAt = now.Date.Add(end);
        if (endAt <= startAt || now <= startAt)
        {
            return 0m;
        }

        var worked = Math.Min((now - startAt).TotalMinutes, (endAt - startAt).TotalMinutes);
        var total = Math.Max(1, (endAt - startAt).TotalMinutes);
        var dailySalary = WorkstationMonthlySalary / 21.75m;
        return dailySalary * (decimal)(worked / total);
    }

    private string GetWorkstationStatusText(DateTime now)
    {
        if (!IsWorkday(now.DayOfWeek))
        {
            return "休息日，别想工作";
        }

        if (!TryParseTime(WorkstationWorkStartTime, out var start) ||
            !TryParseTime(WorkstationWorkEndTime, out var end) ||
            !TryParseTime(WorkstationLunchStartTime, out var lunchStart) ||
            !TryParseTime(WorkstationLunchEndTime, out var lunchEnd))
        {
            return "时间配置需要检查";
        }

        var time = now.TimeOfDay;
        if (time < start)
        {
            return "还没上班";
        }

        if (time >= end)
        {
            return "已下班";
        }

        if (lunchEnd > lunchStart && time >= lunchStart && time < lunchEnd)
        {
            return "午休中";
        }

        return (end - time).TotalMinutes <= 30 ? "即将下班" : "上班中";
    }

    private bool IsWorkday(DayOfWeek dayOfWeek)
    {
        var dayNumber = dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
        return WorkstationWorkdays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => int.TryParse(part, out var value) && value == dayNumber);
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        return TimeSpan.TryParse(value, out time);
    }

    private void NotifyWorkstationPreviewChanged()
    {
        OnPropertyChanged(nameof(WorkstationPreviewStatusText));
        OnPropertyChanged(nameof(WorkstationPreviewTodayEarnedText));
        OnPropertyChanged(nameof(WorkstationPreviewOffWorkText));
        OnPropertyChanged(nameof(WorkstationPreviewPaydayText));
        OnPropertyChanged(nameof(WorkstationPreviewSummaryText));
        NotifyOverworkPreviewChanged();
    }

    private void NotifyOverworkPreviewChanged()
    {
        OnPropertyChanged(nameof(OverworkPreviewStageText));
        OnPropertyChanged(nameof(OverworkPreviewHudColorText));
        OnPropertyChanged(nameof(OverworkPreviewNextReminderText));
        OnPropertyChanged(nameof(OverworkPreviewWorkedText));
    }

    private WorkTimeStageSnapshot GetOverworkPreviewSnapshot(DateTime now)
    {
        var service = new WorkTimeStageService(_settingsService);
        return service.GetSnapshot(now);
    }

    private static string GetStageDisplayName(WorkTimeStage stage) => stage switch
    {
        WorkTimeStage.BeforeWork => "未上班",
        WorkTimeStage.EarlyWork => "工作前段",
        WorkTimeStage.MidWork => "工作中段",
        WorkTimeStage.LateWork => "工作后段",
        WorkTimeStage.Overtime => "加班中",
        WorkTimeStage.LunchBreak => "午休中",
        WorkTimeStage.RestDay => "休息日",
        _ => "已下班"
    };

    private void UpdateGestureSettingsSnapshot()
    {
        _gestureSettingsProvider.Update(new GestureSettings(
            _gestureEnabled,
            _gestureShowOverlay,
            _gestureCloseWindowEnabled,
            _gestureDebugEnabled,
            _selectedGesturePreset,
            new GestureOptions(_gestureTriggerThreshold, 16, 2000, 2),
            _gestureLeftButtonEnabled,
            _gestureMiddleButtonEnabled,
            _gestureXButton1Enabled,
            _gestureXButton2Enabled,
            _gestureRightButtonEnabled));
    }

    private void RefreshGestureBindingCards()
    {
        var previousPattern = SelectedGestureBindingCard?.Pattern;
        GestureBindingCards.Clear();
        PrimaryGestureBindingCards.Clear();
        AdvancedGestureBindingCards.Clear();
        var bindings = _gesturePresetProvider.GetBindings(_selectedGesturePreset);
        var patterns = GesturePatterns
            .Concat(bindings.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(pattern => Array.IndexOf(GesturePatterns, pattern) >= 0 ? Array.IndexOf(GesturePatterns, pattern) : int.MaxValue)
            .ThenBy(pattern => pattern, StringComparer.Ordinal);

        foreach (var pattern in patterns)
        {
            var action = bindings.TryGetValue(pattern, out var mappedAction) ? mappedAction : BuiltInGestureAction.None;
            var card = new GestureBindingCardViewModel(
                pattern,
                DirectionText(pattern),
                GestureName(pattern),
                PrimaryGesturePatterns.Contains(pattern),
                action,
                GestureActionOptions,
                ApplyGestureBindingAsync,
                DeleteGestureBindingAsync);
            GestureBindingCards.Add(card);
            if (card.IsCommon || card.IsBound)
            {
                PrimaryGestureBindingCards.Add(card);
            }
            else
            {
                AdvancedGestureBindingCards.Add(card);
            }
        }

        SelectedGestureBindingCard = PrimaryGestureBindingCards.FirstOrDefault(card => card.Pattern == previousPattern)
            ?? GestureBindingCards.FirstOrDefault(card => card.Pattern == previousPattern)
            ?? PrimaryGestureBindingCards.FirstOrDefault()
            ?? GestureBindingCards.FirstOrDefault();
    }

    private async Task ApplyGestureBindingAsync(GestureBindingCardViewModel card)
    {
        if (ReferenceEquals(card, SelectedGestureBindingCard))
        {
            OnPropertyChanged(nameof(SelectedGestureBindingActionName));
            OnPropertyChanged(nameof(SelectedGestureBindingShortcutText));
            OnPropertyChanged(nameof(SelectedGestureBindingEmptyText));
            RefreshGestureCardBuckets(card);
        }

        await SaveGestureBindingsAsync();
    }

    private void RefreshGestureCardBuckets(GestureBindingCardViewModel card)
    {
        var shouldBePrimary = card.IsCommon || card.IsBound;
        var inPrimary = PrimaryGestureBindingCards.Contains(card);
        if (shouldBePrimary && !inPrimary)
        {
            AdvancedGestureBindingCards.Remove(card);
            PrimaryGestureBindingCards.Add(card);
        }
        else if (!shouldBePrimary && inPrimary)
        {
            PrimaryGestureBindingCards.Remove(card);
            AdvancedGestureBindingCards.Add(card);
        }
    }

    private async Task DeleteGestureBindingAsync(GestureBindingCardViewModel card)
    {
        var index = GestureBindingCards.IndexOf(card);
        GestureBindingCards.Remove(card);
        PrimaryGestureBindingCards.Remove(card);
        AdvancedGestureBindingCards.Remove(card);
        if (ReferenceEquals(card, SelectedGestureBindingCard))
        {
            SelectedGestureBindingCard = GestureBindingCards.Count == 0
                ? null
                : GestureBindingCards[Math.Min(index, GestureBindingCards.Count - 1)];
        }

        await SaveGestureBindingsAsync();
    }

    public Task DeleteSelectedGestureBindingAsync()
    {
        return SelectedGestureBindingCard is null
            ? Task.CompletedTask
            : DeleteGestureBindingAsync(SelectedGestureBindingCard);
    }

    private async Task SaveGestureBindingsAsync()
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
            SelectedGestureBindingCard = existing;
            NewGesturePattern = "";
            return;
        }

        var card = new GestureBindingCardViewModel(
            pattern,
            DirectionText(pattern),
            GestureName(pattern),
            PrimaryGesturePatterns.Contains(pattern),
            NewGestureAction,
            GestureActionOptions,
            ApplyGestureBindingAsync,
            DeleteGestureBindingAsync);
        GestureBindingCards.Add(card);
        if (card.IsCommon || card.IsBound)
        {
            PrimaryGestureBindingCards.Add(card);
        }
        else
        {
            AdvancedGestureBindingCards.Add(card);
        }
        SelectedGestureBindingCard = card;
        NewGesturePattern = "";
        await ApplyGestureBindingAsync(card);
    }

    private void SetNewGesturePattern(object? parameter)
    {
        if (parameter is string pattern)
        {
            NewGesturePattern = pattern;
        }
    }

    private void SetNewGestureAction(object? parameter)
    {
        if (TryParseGestureAction(parameter, out var action))
        {
            NewGestureAction = action;
        }
    }

    private void SetNewGestureTemplate(object? parameter)
    {
        if (parameter is not string text)
        {
            return;
        }

        var parts = text.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        NewGesturePattern = parts[0];
        if (parts.Length == 2 && TryParseGestureAction(parts[1], out var action))
        {
            NewGestureAction = action;
        }
    }

    private void SetSelectedGestureAction(object? parameter)
    {
        if (SelectedGestureBindingCard is not null && TryParseGestureAction(parameter, out var action))
        {
            SelectedGestureBindingCard.SelectedAction = action;
        }
    }

    private static bool TryParseGestureAction(object? parameter, out BuiltInGestureAction action)
    {
        if (parameter is BuiltInGestureAction actionValue)
        {
            action = actionValue;
            return true;
        }

        if (parameter is string text && Enum.TryParse(text, ignoreCase: true, out BuiltInGestureAction parsed))
        {
            action = parsed;
            return true;
        }

        action = BuiltInGestureAction.None;
        return false;
    }

    private void AppendGestureDirection(object? parameter)
    {
        if (parameter is not string direction || direction.Length != 1)
        {
            return;
        }

        var normalized = NormalizeGesturePattern(direction);
        if (!IsValidGesturePattern(normalized))
        {
            return;
        }

        if (NewGesturePattern.Length >= 8)
        {
            return;
        }

        NewGesturePattern += normalized;
    }

    private void RemoveLastGestureDirection()
    {
        if (NewGesturePattern.Length == 0)
        {
            return;
        }

        NewGesturePattern = NewGesturePattern[..^1];
    }

    public void SetNewGesturePatternFromRecordedPoints(IReadOnlyList<GesturePoint> points)
    {
        var recognizer = new DirectionGestureRecognizer();
        var result = recognizer.Recognize(
            points,
            new GestureOptions(
                TriggerThreshold: Math.Max(20, GestureTriggerThreshold),
                SegmentThreshold: 16,
                MaxDurationMs: 5000,
                MinGesturePoints: 2));
        if (!result.IsValid || string.IsNullOrWhiteSpace(result.Pattern))
        {
            RecordGestureStatusText = "没识别出来，画得稍微长一点。";
            return;
        }

        NewGesturePattern = result.Pattern;
        RecordGestureStatusText = $"识别为 {DirectionText(result.Pattern)}，可以直接添加。";
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
        OnPropertyChanged(nameof(EdgeTriggerLastSource));
        OnPropertyChanged(nameof(EdgeTriggerLastPosition));
        OnPropertyChanged(nameof(EdgeTriggerLastAction));
        OnPropertyChanged(nameof(EdgeTriggerLastReason));
        OnPropertyChanged(nameof(EdgeTriggerLastEventTime));
        OnPropertyChanged(nameof(EdgeTriggerCooldownText));
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

    public sealed record WorkstationTemplateOption(
        string Name,
        string WorkStartTime,
        string WorkEndTime,
        string LunchStartTime,
        string LunchEndTime,
        string Workdays);

    public sealed record GestureStrokeColorOption(string Name, string Color)
    {
        public string DisplayName => $"{Name}  {Color}";
    }

    public sealed record GestureTriggerModeViewModel(string Name, string Status, bool IsEnabled);

    public IReadOnlyList<GestureActionOptionViewModel> GestureActionOptions { get; } = GestureActionCatalog.DefaultOptions;

    public ICollectionView GestureActionOptionsView { get; }

    private static readonly string[] GesturePatterns =
    [
        "U", "D", "UD", "DU", "L", "R", "LR", "RL", "DL", "DR",
        "UR", "UL", "RU", "RD", "LD", "RDL", "RUD", "URD", "ULD", "RULD"
    ];

    private static readonly HashSet<string> PrimaryGesturePatterns = new(StringComparer.Ordinal)
    {
        "U", "D", "UD", "DU", "L", "R", "LR", "RL", "DL", "DR"
    };

    private static string NormalizeGesturePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "";
        }

        var normalized = pattern.Trim().ToUpperInvariant();
        if (normalized is "R+L" or "右键+左键" or "右键按住+左键点击")
        {
            return "R+L";
        }

        return new string(normalized.Where(ch => ch is 'U' or 'D' or 'L' or 'R').ToArray());
    }

    private static bool IsValidGesturePattern(string pattern)
    {
        return string.Equals(pattern, "R+L", StringComparison.Ordinal) ||
            pattern.Length is >= 1 and <= 8 && pattern.All(ch => ch is 'U' or 'D' or 'L' or 'R');
    }

    private static string DirectionText(string pattern) => pattern == "R+L"
        ? "右键 + 左键"
        : pattern
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
        "DL" => "下左划",
        "R+L" => "右键按住 + 左键点击",
        "DR" => "下右划",
        "UR" => "上右划",
        "UL" => "上左划",
        "RU" => "右上划",
        "RD" => "右下划",
        "LD" => "左下划",
        "RDL" => "右下左划",
        "RUD" => "右上下划",
        "URD" => "上右下划",
        "ULD" => "上左下划",
        "RULD" => "右上左下划",
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
        BuiltInGestureAction.NextTab => "Ctrl+Tab",
        BuiltInGestureAction.PreviousTab => "Ctrl+Shift+Tab",
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
        BuiltInGestureAction.LeftMouseClick => "LeftClick",
        BuiltInGestureAction.LeftMouseDoubleClick => "LeftDoubleClick",
        BuiltInGestureAction.RightMouseClick => "RightClick",
        BuiltInGestureAction.MiddleMouseClick => "MiddleClick",
        BuiltInGestureAction.MouseWheelUp => "WheelUp",
        BuiltInGestureAction.MouseWheelDown => "WheelDown",
        BuiltInGestureAction.SearchSelectedTextWithGoogle => "GoogleSearch",
        BuiltInGestureAction.SearchSelectedTextWithBaidu => "BaiduSearch",
        BuiltInGestureAction.SearchSelectedTextWithBing => "BingSearch",
        BuiltInGestureAction.OpenGoogle => "Google",
        BuiltInGestureAction.OpenBaidu => "Baidu",
        _ => ""
    };

    private sealed record CustomBindingDto(BuiltInGestureAction Action, string Shortcut, bool IsEnabled);
}
