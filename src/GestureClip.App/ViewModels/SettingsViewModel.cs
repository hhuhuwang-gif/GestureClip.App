using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
using GestureClip.App.Services;
using GestureClip.Infrastructure.Paths;
using System.Windows.Data;
using System.Windows.Threading;

namespace GestureClip.App.ViewModels;

public sealed partial class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly AppThemeService? _themeService;
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
    private bool _isDarkTheme;
    private bool _clipboardCaptureEnabled;
    private bool _gestureEnabled;
    private bool _gestureShowOverlay;
    private bool _gestureDebugEnabled;
    private bool _gestureCloseWindowEnabled;
    private bool _isSmartPasteEnabled;
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
    private string _openClipboardHotkeyText = HotkeyDefinition.DefaultOpenClipboardOverlay;
    private string _openQuickActionHotkeyText = HotkeyDefinition.DefaultOpenQuickActionCenter;
    private string _pastePlainTextHotkeyText = HotkeyDefinition.DefaultPastePlainText;
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
    private bool _autoShowDailyWorkReport;
    private bool _enableFishMode;
    private bool _enableWorkSprintMode;
    private bool _enableWorkBearShareCard;
    private bool _enableWorkBearHudStatusText;
    private bool _enableWorkBearHudThemeColor;
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
    private bool _newGestureAddConfirmationPending;
    private string _recordGestureStatusText = "按住左键在方框里画一次。";
    private string _recommendedGestureStatusText = "已有的自定义手势不会被删除；已存在的手势会自动跳过。";
    private string _workerLevelText = "Lv.1 初入工位";
    private string _workerXpText = "XP 0 / 50";
    private bool _workerLevelShowLevelUpPopup;
    private bool _workerLevelShowLevelInHud;
    private bool _hudFunTextEnabled;
    private bool _hudStatusLevelEnabled;
    private string _lastDiagnosticsExportText = "尚未导出诊断包";
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
        IWorkerLevelService workerLevelService,
        AppThemeService? themeService = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
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
        _isSmartPasteEnabled = _settingsService.Get(SettingKeys.SmartPasteEnabled, true);
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
        _workstationDailyReportEnabled = _settingsService.Get(SettingKeys.EnableWorkReport, _settingsService.Get(SettingKeys.WorkstationDailyReportEnabled, false));
        _autoShowDailyWorkReport = _settingsService.Get(SettingKeys.AutoShowDailyWorkReport, true);
        _enableFishMode = _settingsService.Get(SettingKeys.EnableFishMode, true);
        _enableWorkSprintMode = _settingsService.Get(SettingKeys.EnableWorkSprintMode, true);
        _enableWorkBearShareCard = _settingsService.Get(SettingKeys.EnableWorkBearShareCard, true);
        _enableWorkBearHudStatusText = _settingsService.Get(SettingKeys.EnableWorkBearHudStatusText, true);
        _enableWorkBearHudThemeColor = _settingsService.Get(SettingKeys.EnableWorkBearHudThemeColor, true);
        _workstationCopywritingStyle = _settingsService.Get(SettingKeys.WorkBearTextStyle, _settingsService.Get(SettingKeys.WorkstationCopywritingStyle, "打工人模式"));
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
        var quickActionHotkeyText = _settingsService.Get(
            SettingKeys.HotkeyOpenQuickActionCenterKey,
            HotkeyDefinition.DefaultOpenQuickActionCenter);
        _openQuickActionHotkeyText = HotkeyDefinition.TryParse(quickActionHotkeyText, out var quickActionHotkey)
            ? quickActionHotkey.DisplayText
            : HotkeyDefinition.DefaultOpenQuickActionCenter;
        var pastePlainHotkeyText = _settingsService.Get(
            SettingKeys.HotkeyPastePlainTextKey,
            HotkeyDefinition.DefaultPastePlainText);
        _pastePlainTextHotkeyText = HotkeyDefinition.TryParse(pastePlainHotkeyText, out var pastePlainHotkey)
            ? pastePlainHotkey.DisplayText
            : HotkeyDefinition.DefaultPastePlainText;
        _gestureStrokeColor = _settingsService.Get(SettingKeys.GestureStrokeColor, "#8CC8FF");
        _gestureDiagnostics = _mouseGestureService.Diagnostics;
        _startWithWindows = _startupService.IsEnabled();
        _isDarkTheme = string.Equals(
            _settingsService.Get(SettingKeys.UiThemeMode, "Light"),
            "Dark",
            StringComparison.OrdinalIgnoreCase);
        StartupModeWarning = _startupService.IsDevelopmentRunMode()
            ? "当前看起来是开发运行路径，开机自启建议在发布版中开启。"
            : "";
        AddBlacklistItemCommand = new AsyncRelayCommand(_ => AddBlacklistItemAsync());
        RefreshDiagnosticsCommand = new AsyncRelayCommand(_ => RefreshDiagnosticsAsync());
        CopyDiagnosticsCommand = new AsyncRelayCommand(_ => CopyDiagnosticsAsync());
        ExportDiagnosticsCommand = new AsyncRelayCommand(_ => ExportDiagnosticsAsync());
        RefreshClipboardStatsCommand = new AsyncRelayCommand(_ => RefreshClipboardStatsAsync());
        ClearAllClipboardItemsCommand = new AsyncRelayCommand(_ => ClearAllClipboardItemsAsync());
        ClearUnpinnedClipboardItemsCommand = new AsyncRelayCommand(_ => ClearUnpinnedClipboardItemsAsync());
        ApplyClipboardCleanupCommand = new AsyncRelayCommand(_ => ApplyClipboardCleanupAsync());
        ApplyRecommendedGestureBindingsCommand = new AsyncRelayCommand(_ => ApplyRecommendedGestureBindingsAsync());
        AddCustomGestureBindingCommand = new AsyncRelayCommand(_ => AddCustomGestureBindingAsync());
        DeleteSelectedGestureBindingCommand = new AsyncRelayCommand(_ => DeleteSelectedGestureBindingAsync());
        AddLeftButtonEnhancedBindingCommand = new RelayCommand(_ => AddLeftButtonEnhancedBinding());
        ResetLeftButtonEnhancedBindingsCommand = new AsyncRelayCommand(_ => ResetLeftButtonEnhancedBindingsAsync());
        SetNewGesturePatternCommand = new RelayCommand(SetNewGesturePattern);
        SetNewGestureActionCommand = new RelayCommand(SetNewGestureAction);
        SetNewGestureTemplateCommand = new RelayCommand(SetNewGestureTemplate);
        SetSelectedGestureActionCommand = new RelayCommand(SetSelectedGestureAction);
        SelectGestureBindingCommand = new RelayCommand(SelectGestureBinding);
        AppendGestureDirectionCommand = new RelayCommand(AppendGestureDirection);
        RemoveLastGestureDirectionCommand = new RelayCommand(_ => RemoveLastGestureDirection());
        ClearNewGesturePatternCommand = new RelayCommand(_ => ClearNewGesturePattern());
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
        RefreshLeftButtonEnhancedBindings();
        LoadChangelogText();
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

    public string OpenQuickActionHotkeyText
    {
        get => _openQuickActionHotkeyText;
        set
        {
            var hotkey = HotkeyDefinition.TryParse(value, out var parsed)
                ? parsed
                : HotkeyDefinition.ParseOrDefault(HotkeyDefinition.DefaultOpenQuickActionCenter);
            if (_openQuickActionHotkeyText == hotkey.DisplayText)
            {
                return;
            }

            _openQuickActionHotkeyText = hotkey.DisplayText;
            OnPropertyChanged();
            _ = ApplyOpenQuickActionHotkeyAsync(hotkey.DisplayText);
        }
    }

    public string PastePlainTextHotkeyText
    {
        get => _pastePlainTextHotkeyText;
        set
        {
            if (!HotkeyDefinition.TryParse(value, out var hotkey))
            {
                hotkey = new HotkeyDefinition(
                    HotkeyModifier.Control | HotkeyModifier.Shift,
                    (uint)'V',
                    HotkeyDefinition.DefaultPastePlainText);
            }

            if (_pastePlainTextHotkeyText == hotkey.DisplayText)
            {
                return;
            }

            _pastePlainTextHotkeyText = hotkey.DisplayText;
            OnPropertyChanged();
            _ = ApplyPastePlainTextHotkeyAsync(hotkey.DisplayText);
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (_isDarkTheme == value)
            {
                return;
            }

            _isDarkTheme = value;
            OnPropertyChanged();
            _ = ApplyThemeAsync(value);
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

    public ObservableCollection<LeftButtonEnhancedBindingViewModel> LeftButtonEnhancedBindings { get; } = [];

    public IReadOnlyList<RecommendedGestureBindingViewModel> RecommendedGestureBindings { get; } = BuildRecommendedGestureBindings();

    public string RecommendedGestureStatusText
    {
        get => _recommendedGestureStatusText;
        private set
        {
            if (_recommendedGestureStatusText == value)
            {
                return;
            }

            _recommendedGestureStatusText = value;
            OnPropertyChanged();
        }
    }

    public GestureBindingCardViewModel? SelectedGestureBindingCard
    {
        get => _selectedGestureBindingCard;
        set
        {
            if (ReferenceEquals(_selectedGestureBindingCard, value))
            {
                return;
            }

            if (_selectedGestureBindingCard is not null)
            {
                _selectedGestureBindingCard.IsSelected = false;
            }

            _selectedGestureBindingCard = value;
            if (_selectedGestureBindingCard is not null)
            {
                _selectedGestureBindingCard.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedGestureBinding));
            OnPropertyChanged(nameof(SelectedGestureBindingPattern));
            OnPropertyChanged(nameof(SelectedGestureBindingDirectionText));
            OnPropertyChanged(nameof(SelectedGestureBindingActionName));
            OnPropertyChanged(nameof(SelectedGestureBindingShortcutText));
            OnPropertyChanged(nameof(SelectedGestureBindingEmptyText));
            OnPropertyChanged(nameof(SelectedGestureBindingSelectionKey));
        }
    }

    public string SelectedGestureBindingSelectionKey
    {
        get => SelectedGestureBindingCard?.Pattern ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var card = GestureBindingCards.FirstOrDefault(item =>
                string.Equals(item.Pattern, value, StringComparison.Ordinal));
            if (card is not null)
            {
                SelectedGestureBindingCard = card;
            }
        }
    }

    public bool HasSelectedGestureBinding => SelectedGestureBindingCard is not null;

    public string SelectedGestureBindingPattern => SelectedGestureBindingCard?.Pattern ?? "-";

    public string SelectedGestureBindingDirectionText => SelectedGestureBindingCard?.DirectionText ?? "先从左侧选择一个手势";

    public string SelectedGestureBindingActionName => SelectedGestureBindingCard?.ActionName ?? "-";

    public string SelectedGestureBindingShortcutText => SelectedGestureBindingCard?.ShortcutText ?? "";

    public string SelectedGestureBindingEmptyText => SelectedGestureBindingCard is null
        ? "还没有选中手势。请先从上方卡片选择一个手势，再更换动作或删除。"
        : SelectedGestureBindingCard.SelectedAction == BuiltInGestureAction.None
            ? "该手势尚未绑定动作，选择一个动作进行绑定。"
            : "修改后会自动保存。";

    public string GestureBindingEmptyStateText => GestureBindingCards.Count == 0
        ? "还没有自定义手势。你可以先使用推荐配置，也可以添加一个自己的手势。"
        : string.Empty;

    public string CustomGestureEmptyStateText => HasCustomGestureCards
        ? string.Empty
        : "还没有自定义手势。你可以先使用推荐配置，也可以添加一个自己的手势。";

    private bool HasCustomGestureCards => GestureBindingCards.Any(card => !GesturePatterns.Contains(card.Pattern));

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

    public ICommand ExportDiagnosticsCommand { get; }

    public ICommand OpenLogDirectoryCommand { get; }

    public ICommand OpenDataDirectoryCommand { get; }

    public ICommand RefreshClipboardStatsCommand { get; }

    public ICommand ClearAllClipboardItemsCommand { get; }

    public ICommand ClearUnpinnedClipboardItemsCommand { get; }

    public ICommand ApplyClipboardCleanupCommand { get; }

    public ICommand ApplyRecommendedGestureBindingsCommand { get; }

    public ICommand AddCustomGestureBindingCommand { get; }

    public ICommand DeleteSelectedGestureBindingCommand { get; }

    public ICommand AddLeftButtonEnhancedBindingCommand { get; }

    public ICommand ResetLeftButtonEnhancedBindingsCommand { get; }

    public string LeftButtonEnhancedStatusText { get; private set; } =
        "按住右键画手势时，再点一下左键，会执行这里配置的增强动作。";

    public string ChangelogText { get; private set; } = "更新日志加载中…";

    public string AppVersionText
    {
        get
        {
            var version = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
                ?? "unknown";
            var plus = version.IndexOf('+');
            return plus > 0 ? version[..plus] : version;
        }
    }

    public ICommand SetNewGesturePatternCommand { get; }

    public ICommand SetNewGestureActionCommand { get; }

    public ICommand SetNewGestureTemplateCommand { get; }

    public ICommand SetSelectedGestureActionCommand { get; }

    public ICommand SelectGestureBindingCommand { get; }

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
            _ = _settingsService.SetAsync(SettingKeys.WorkBearTextStyle, normalized, CancellationToken.None);
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
            _ = _settingsService.SetAsync(SettingKeys.EnableWorkReport, value, CancellationToken.None);
        }
    }

    public bool AutoShowDailyWorkReport
    {
        get => _autoShowDailyWorkReport;
        set
        {
            if (_autoShowDailyWorkReport == value) return;
            _autoShowDailyWorkReport = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.AutoShowDailyWorkReport, value, CancellationToken.None);
        }
    }

    public bool EnableFishMode
    {
        get => _enableFishMode;
        set
        {
            if (_enableFishMode == value) return;
            _enableFishMode = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.EnableFishMode, value, CancellationToken.None);
        }
    }

    public bool EnableWorkSprintMode
    {
        get => _enableWorkSprintMode;
        set
        {
            if (_enableWorkSprintMode == value) return;
            _enableWorkSprintMode = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.EnableWorkSprintMode, value, CancellationToken.None);
        }
    }

    public bool EnableWorkBearShareCard
    {
        get => _enableWorkBearShareCard;
        set
        {
            if (_enableWorkBearShareCard == value) return;
            _enableWorkBearShareCard = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.EnableWorkBearShareCard, value, CancellationToken.None);
        }
    }

    public bool EnableWorkBearHudStatusText
    {
        get => _enableWorkBearHudStatusText;
        set
        {
            if (_enableWorkBearHudStatusText == value) return;
            _enableWorkBearHudStatusText = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.EnableWorkBearHudStatusText, value, CancellationToken.None);
        }
    }

    public bool EnableWorkBearHudThemeColor
    {
        get => _enableWorkBearHudThemeColor;
        set
        {
            if (_enableWorkBearHudThemeColor == value) return;
            _enableWorkBearHudThemeColor = value;
            _workstationEnableHudTimeColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WorkstationEnableHudTimeColor));
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.EnableWorkBearHudThemeColor, value, CancellationToken.None);
            _ = _settingsService.SetAsync(SettingKeys.WorkstationEnableHudTimeColor, value, CancellationToken.None);
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
            _enableWorkBearHudThemeColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EnableWorkBearHudThemeColor));
            NotifyOverworkPreviewChanged();
            _ = _settingsService.SetAsync(SettingKeys.WorkstationEnableHudTimeColor, value, CancellationToken.None);
            _ = _settingsService.SetAsync(SettingKeys.EnableWorkBearHudThemeColor, value, CancellationToken.None);
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

    public bool IsSmartPasteEnabled
    {
        get => _isSmartPasteEnabled;
        set
        {
            if (_isSmartPasteEnabled == value)
            {
                return;
            }

            _isSmartPasteEnabled = value;
            OnPropertyChanged();
            _ = _settingsService.SetAsync(SettingKeys.SmartPasteEnabled, value, CancellationToken.None);
        }
    }

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

    public string LastDiagnosticsExportText
    {
        get => _lastDiagnosticsExportText;
        private set
        {
            if (_lastDiagnosticsExportText == value)
            {
                return;
            }

            _lastDiagnosticsExportText = value;
            OnPropertyChanged();
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
            ResetNewGestureAddConfirmation();
            OnPropertyChanged();
            OnPropertyChanged(nameof(NewGestureDirectionPreview));
            OnPropertyChanged(nameof(NewGestureAddButtonText));
        }
    }

    public string NewGestureDirectionPreview => string.IsNullOrEmpty(NewGesturePattern)
        ? "点击方向按钮设计手势"
        : DirectionText(NewGesturePattern);

    public string NewGestureAddButtonText => string.IsNullOrEmpty(NewGesturePattern)
        ? "先画一个手势"
        : _newGestureAddConfirmationPending
            ? "确认添加到手势列表"
            : "确认添加到手势列表";

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
            ResetNewGestureAddConfirmation();
            OnPropertyChanged();
            OnPropertyChanged(nameof(NewGestureAddButtonText));
        }
    }

    public GestureActionOptionViewModel? NewGestureActionOption
    {
        get => GestureActionOptions.FirstOrDefault(o => o.Action == NewGestureAction);
        set
        {
            if (value is null || value.Action == NewGestureAction)
            {
                return;
            }

            NewGestureAction = value.Action;
        }
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


    private async Task ApplyThemeAsync(bool dark)
    {
        var mode = dark ? AppUiThemeMode.Dark : AppUiThemeMode.Light;
        if (_themeService is not null)
        {
            await _themeService.SetModeAsync(mode);
            return;
        }

        await _settingsService.SetAsync(SettingKeys.UiThemeMode, dark ? "Dark" : "Light", CancellationToken.None);
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


    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
