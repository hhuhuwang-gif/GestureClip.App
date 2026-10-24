using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class EdgeTriggerService : IEdgeTriggerService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(80);

    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly ILowLevelMouseHook _mouseHook;
    private readonly IMouseGestureActionExecutor _actionExecutor;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<EdgeTriggerService> _logger;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private ScreenCornerTarget? _activeCorner;
    private DateTimeOffset? _enteredCornerAt;
    private ScreenEdgeTarget? _activeSlideEdge;
    private CursorPosition? _slideStartPoint;
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;
    private EdgeTriggerSettings _currentSettings;
    private bool _started;
    private EdgeTriggerDiagnosticsSnapshot _diagnostics = new(false, "-", "-", BuiltInGestureAction.None, "未触发", null, null);

    public EdgeTriggerService(
        ICursorPositionProvider cursorPositionProvider,
        ILowLevelMouseHook mouseHook,
        IMouseGestureActionExecutor actionExecutor,
        ISettingsService settingsService,
        ILogger<EdgeTriggerService> logger)
    {
        _cursorPositionProvider = cursorPositionProvider;
        _mouseHook = mouseHook;
        _actionExecutor = actionExecutor;
        _settingsService = settingsService;
        _logger = logger;
        _currentSettings = LoadSettings();
        IsEnabled = _currentSettings.Enabled;
    }

    public bool IsEnabled { get; private set; }

    public EdgeTriggerDiagnosticsSnapshot Diagnostics
    {
        get
        {
            lock (_syncRoot)
            {
                return _diagnostics;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = RefreshSettingsCore();
        IsEnabled = settings.Enabled;
        if (!IsEnabled)
        {
            SetDiagnostics("服务状态", "-", BuiltInGestureAction.None, "边缘触发未启用", null);
            return;
        }

        lock (_syncRoot)
        {
            if (_loopTask is { IsCompleted: false })
            {
                return;
            }

            var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var loopToken = loopCancellation.Token;
            _loopCancellation = loopCancellation;
            _mouseHook.MouseEventReceived += OnMouseEventReceived;
            _started = true;
            _loopTask = Task.Run(() => RunAsync(loopToken), CancellationToken.None);
        }

        try
        {
            await _mouseHook.StartAsync(cancellationToken);
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }

        _logger.LogInformation("Edge trigger service started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var (cancellation, loopTask) = DetachLoop();
        if (cancellation is null && loopTask is null)
        {
            IsEnabled = false;
            return;
        }

        _mouseHook.MouseEventReceived -= OnMouseEventReceived;
        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
            cancellation.Dispose();
        }

        await WaitForLoopShutdownAsync(loopTask, cancellationToken);
        await _mouseHook.StopAsync(cancellationToken);
        IsEnabled = false;
    }

    private (CancellationTokenSource? Cancellation, Task? LoopTask) DetachLoop()
    {
        lock (_syncRoot)
        {
            var cancellation = _loopCancellation;
            var loopTask = _loopTask;
            _loopCancellation = null;
            _loopTask = null;
            if (!_started)
            {
                cancellation = null;
                loopTask = null;
            }

            _started = false;
            ResetTracking();
            SetDiagnostics("服务状态", "-", BuiltInGestureAction.None, "边缘触发已停止", null);
            return (cancellation, loopTask);
        }
    }

    private async Task WaitForLoopShutdownAsync(Task? loopTask, CancellationToken cancellationToken)
    {
        if (loopTask is null)
        {
            return;
        }

        try
        {
            await loopTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Edge trigger service did not stop within timeout.");
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        EdgeTriggerSettings settings;
        lock (_syncRoot)
        {
            settings = _currentSettings;
        }

        IsEnabled = settings.Enabled;
        if (!settings.Enabled)
        {
            lock (_syncRoot)
            {
                ResetTracking();
                SetDiagnostics("轮询", "-", BuiltInGestureAction.None, "边缘触发未启用", null);
            }

            return;
        }

        var position = _cursorPositionProvider.GetCurrentPosition();
        var bounds = _cursorPositionProvider.GetVirtualScreenBounds();
        await EvaluateAsync(position, bounds, settings, cancellationToken);
    }

    public void RefreshSettings() => RefreshSettingsCore();

    private EdgeTriggerSettings RefreshSettingsCore()
    {
        var settings = LoadSettings();
        lock (_syncRoot)
        {
            _currentSettings = settings;
            if (!settings.Enabled)
            {
                ResetTracking();
            }

            IsEnabled = settings.Enabled;
        }

        return settings;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge trigger loop failed.");
        }
    }
}
