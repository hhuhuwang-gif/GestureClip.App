using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private const int MaxTrackedPoints = 96;
    private const int MaxPreviewPatternLength = 2;
    private const int HudRefreshIntervalMs = 24;

    private readonly ILowLevelMouseHook _mouseHook;
    private readonly IMouseGestureRecognizer _recognizer;
    private readonly IMouseGestureActionExecutor _actionExecutor;
    private readonly IRightClickSynthesizer _rightClickSynthesizer;
    private readonly IGestureSettingsProvider _settingsProvider;
    private readonly IGesturePresetProvider _presetProvider;
    private readonly IGestureHudInfoProvider _hudInfoProvider;
    private readonly IGestureOverlayService _gestureOverlayService;
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IAppBlacklistService _appBlacklistService;
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly IWorkerLevelService _workerLevelService;
    private readonly IWorkerLevelUpService _workerLevelUpService;
    private readonly ILogger<MouseGestureService> _logger;
    private readonly object _syncRoot = new();
    private int _started;
    private GestureRuntimeState _state = GestureRuntimeState.Idle;
    private GestureSettings? _activeSettings;
    private List<GesturePoint> _points = [];
    private GesturePoint? _startPoint;
    private DateTimeOffset? _gestureStartedAt;
    private DateTimeOffset _ignoreInjectedUntil = DateTimeOffset.MinValue;
    /// <summary>
    /// After a successful right-button gesture, swallow residual right-button events so
    /// apps do not open the context menu (synthetic RIGHTUP / delayed physical up).
    /// </summary>
    private DateTimeOffset _suppressRightButtonUntil = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMoveDebugLogAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastHudRefreshAt = DateTimeOffset.MinValue;
    private bool _traceDirty;
    private bool _synthesizeRightClickOnNextRightButtonUp;
    private GestureTriggerButton _pendingSyntheticClickButton = GestureTriggerButton.Right;
    private GesturePoint? _pendingSyntheticClickPoint;
    private GestureTriggerButton _activeTriggerButton = GestureTriggerButton.Right;
    private bool _rightLeftRockerDown;
    /// <summary>Wheel gesture fired during this hold; release must not run pattern/synthesize click.</summary>
    private bool _wheelGestureFired;
    private bool _leftClickConfirmDown;
    private bool _leftButtonModifier;
    private GestureTriggerButton? _suppressNextButtonUp;
    private string _hookStatus = "未安装";
    private string? _lastPattern;
    private BuiltInGestureAction _lastAction = BuiltInGestureAction.None;
    private string? _lastError;
    private DateTimeOffset? _lastEventAt;
    /// <summary>Foreground HWND at right-button-down; re-focused before paste injection.</summary>
    private nint _gestureTargetHwnd;
    private string? _gestureTargetProcessName;

    public MouseGestureService(
        ILowLevelMouseHook mouseHook,
        IMouseGestureRecognizer recognizer,
        IMouseGestureActionExecutor actionExecutor,
        IRightClickSynthesizer rightClickSynthesizer,
        IGestureSettingsProvider settingsProvider,
        IGesturePresetProvider presetProvider,
        IGestureHudInfoProvider hudInfoProvider,
        IGestureOverlayService gestureOverlayService,
        IForegroundAppService foregroundAppService,
        IAppBlacklistService appBlacklistService,
        IWorkstationDashboardService workstationDashboardService,
        IWorkerLevelService workerLevelService,
        IWorkerLevelUpService workerLevelUpService,
        ILogger<MouseGestureService> logger)
    {
        _mouseHook = mouseHook;
        _recognizer = recognizer;
        _actionExecutor = actionExecutor;
        _rightClickSynthesizer = rightClickSynthesizer;
        _settingsProvider = settingsProvider;
        _presetProvider = presetProvider;
        _hudInfoProvider = hudInfoProvider;
        _gestureOverlayService = gestureOverlayService;
        _foregroundAppService = foregroundAppService;
        _appBlacklistService = appBlacklistService;
        _workstationDashboardService = workstationDashboardService;
        _workerLevelService = workerLevelService;
        _workerLevelUpService = workerLevelUpService;
        _logger = logger;
        IsEnabled = !IsDisabledByEnvironment() && _settingsProvider.GetCurrent().Enabled;
    }

    public bool IsEnabled { get; private set; }

    public GestureDiagnosticsSnapshot Diagnostics
    {
        get
        {
            lock (_syncRoot)
            {
                return new GestureDiagnosticsSnapshot(
                    _hookStatus,
                    _state,
                    _lastPattern,
                    _lastAction,
                    _lastError,
                    _lastEventAt,
                    IsDisabledByEnvironment());
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsDisabledByEnvironment())
        {
            IsEnabled = false;
            SetHookStatus("被环境变量禁用");
            _logger.LogInformation("Mouse gesture disabled by environment variable");
            return Task.CompletedTask;
        }

        var settings = _settingsProvider.GetCurrent();
        IsEnabled = settings.Enabled;
        if (!IsEnabled)
        {
            SetHookStatus("未安装");
            LogDebug(settings, "Gesture disabled by setting");
            return Task.CompletedTask;
        }

        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _mouseHook.MouseEventReceived += OnMouseEventReceived;

        try
        {
            _mouseHook.Start();
            SetHookStatus("已安装");
            LogDebug(settings, "Hook installed");
        }
        catch (Exception ex)
        {
            _mouseHook.MouseEventReceived -= OnMouseEventReceived;
            Interlocked.Exchange(ref _started, 0);
            SetHookStatus("安装失败");
            SetLastError(ex.Message);
            _logger.LogError(ex, "Hook failed");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        IsEnabled = false;
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return Task.CompletedTask;
        }

        _mouseHook.MouseEventReceived -= OnMouseEventReceived;
        _mouseHook.Stop();

        lock (_syncRoot)
        {
            ResetState("StateReset");
            _hookStatus = "未安装";
        }

        return Task.CompletedTask;
    }
}
