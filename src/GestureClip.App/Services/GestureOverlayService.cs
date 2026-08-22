using System.Windows;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.Services;

public sealed class GestureOverlayService : IGestureOverlayService
{
    private const int MaxVisiblePointCount = 96;
    private const int HideFadeMilliseconds = 90;
    private const int HudFrameIntervalMs = 24;

    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private GestureOverlayWindow? _window;
    private GestureOverlayViewModel? _viewModel;
    private CancellationTokenSource? _hideCts;
    private DateTimeOffset _lastUpdateAt = DateTimeOffset.MinValue;
    private readonly object _updateSyncRoot = new();
    private IReadOnlyList<GesturePoint>? _pendingUpdatePoints;
    private GestureHudInfo? _pendingUpdateHudInfo;
    private bool _updateQueued;
    private int _generation;
    private readonly IWorkstationHudService _workstationHudService;
    private DateTimeOffset _lastWorkstationSnapshotAt = DateTimeOffset.MinValue;
    private int _workstationSnapshotQueued;
    private Rect _lastWindowBounds = Rect.Empty;
    private string? _lastStrokeColorText;
    private System.Windows.Media.Brush? _lastStrokeBrush;
    private string? _lastHudThemeKey;
    private System.Windows.Media.Brush? _lastHudBackgroundBrush;
    private string? _lastHudAccentColorText;
    private System.Windows.Media.Brush? _lastHudAccentBrush;

    public GestureOverlayService(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        IWorkstationHudService workstationHudService)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _workstationHudService = workstationHudService;
    }

    public async Task ShowGestureStartAsync(GesturePoint point, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _generation);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            EnsureWindow();
            PositionWindow();
            _hideCts?.Cancel();
            ApplyHudInfo(hudInfo, refreshStyle: true);
            _viewModel!.Points = ToPointCollection([point]);
            ShowWindow();
        });
    }

    public Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        points = TrimVisiblePoints(points);
        var generation = Volatile.Read(ref _generation);

        lock (_updateSyncRoot)
        {
            _pendingUpdatePoints = points;
            _pendingUpdateHudInfo = hudInfo;
            if (_updateQueued)
            {
                return Task.CompletedTask;
            }

            _updateQueued = true;
        }

        _ = QueuePendingUpdateAsync(generation);
        return Task.CompletedTask;
    }

    public async Task CompleteGestureAsync(GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _generation);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            EnsureWindow();
            var isUnbound = IsUnboundHud(hudInfo);
            ApplyHudInfo(hudInfo, refreshStyle: false);
            ShowWindow();
            _window?.PlayTrailFeedback(!isUnbound);
            QueueWorkstationSnapshotRefresh(hudInfo);
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            _ = HideLaterAsync(_hideCts.Token);
        });
    }

    public async Task ShowPatternAsync(string pattern, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _generation);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            EnsureWindow();
            PositionWindow();
            _viewModel!.DirectionText = pattern;
            _viewModel.Pattern = pattern;
            _viewModel.ActionName = "";
            _viewModel.ShortcutText = "";
            ShowWindow();
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            _ = HideLaterAsync(_hideCts.Token);
        });
    }

    public async Task HideAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _generation);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            _hideCts?.Cancel();
            if (_window is not null)
            {
                _window.IsHitTestVisible = false;
                _window.Focusable = false;
            }

            FadeOutAndHideWindow();
        });
    }

    private async Task QueuePendingUpdateAsync(int generation)
    {
        try
        {
            var elapsedMs = (DateTimeOffset.UtcNow - _lastUpdateAt).TotalMilliseconds;
            var delayMs = Math.Max(0, HudFrameIntervalMs - (int)elapsedMs);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                ClearPendingUpdate();
                return;
            }

            await dispatcher.InvokeAsync(
                () => FlushPendingUpdate(generation),
                DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            ClearPendingUpdate();
            System.Diagnostics.Trace.TraceError("Gesture HUD frame update failed: {0}", ex);
        }
    }

    private void FlushPendingUpdate(int generation)
    {
#if DEBUG
        var hudWatch = System.Diagnostics.Stopwatch.StartNew();
#endif
        IReadOnlyList<GesturePoint>? points;
        GestureHudInfo? hudInfo;
        lock (_updateSyncRoot)
        {
            points = _pendingUpdatePoints;
            hudInfo = _pendingUpdateHudInfo;
            _pendingUpdatePoints = null;
            _pendingUpdateHudInfo = null;
            _updateQueued = false;
        }

        if (points is null || hudInfo is null || generation != Volatile.Read(ref _generation))
        {
            return;
        }

        _lastUpdateAt = DateTimeOffset.UtcNow;
        EnsureWindow();
        ApplyHudInfo(hudInfo, refreshStyle: false);
        _viewModel!.Points = ToPointCollection(points);
        ShowWindow();
#if DEBUG
        hudWatch.Stop();
        System.Diagnostics.Trace.WriteLine(
            $"GesturePerf HudUpdateDurationMs ElapsedMs={hudWatch.ElapsedMilliseconds} PointCount={points.Count}");
#endif
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.BeginAnimation(UIElement.OpacityProperty, null);
        _window.Opacity = 1;
        _window.Show();
    }

    private void FadeOutAndHideWindow()
    {
        if (_window is null || !_window.IsVisible)
        {
            return;
        }

        var window = _window;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(HideFadeMilliseconds))
        {
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_window, window))
            {
                return;
            }

            window.Opacity = 1;
            window.Hide();
        };

        window.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void ClearPendingUpdate()
    {
        lock (_updateSyncRoot)
        {
            _pendingUpdatePoints = null;
            _pendingUpdateHudInfo = null;
            _updateQueued = false;
        }
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _viewModel = _serviceProvider.GetRequiredService<GestureOverlayViewModel>();
        _window = _serviceProvider.GetRequiredService<GestureOverlayWindow>();
        _window.DataContext = _viewModel;
        _window.IsHitTestVisible = false;
        _window.Focusable = false;
        _window.Closed += (_, _) =>
        {
            _window = null;
            _viewModel = null;
        };
    }

    private void ApplyHudInfo(GestureHudInfo hudInfo, bool refreshStyle)
    {
        _viewModel!.DirectionText = hudInfo.DirectionText;
        _viewModel.Pattern = hudInfo.Pattern;
        _viewModel.ActionName = hudInfo.ActionName;
        _viewModel.ShortcutText = hudInfo.ShortcutText;
        _viewModel.PresetName = hudInfo.PresetName;
        if (refreshStyle || _lastStrokeBrush is null)
        {
            _viewModel.StrokeBrush = GetStrokeBrush(_settingsService.Get(SettingKeys.GestureStrokeColor, "#8CC8FF"));
        }
        if (_viewModel.StrokeBrush is SolidColorBrush strokeSolid)
        {
            _viewModel.StrokeGlowColor = strokeSolid.Color;
        }
    }

    private static bool IsUnboundHud(GestureHudInfo hudInfo)
    {
        if (string.IsNullOrWhiteSpace(hudInfo.Pattern) || hudInfo.Pattern == "-")
        {
            return false;
        }

        return hudInfo.Action == BuiltInGestureAction.None
            || string.Equals(hudInfo.ActionName, "未绑定", StringComparison.Ordinal);
    }

    private void QueueWorkstationSnapshotRefresh(GestureHudInfo hudInfo)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastWorkstationSnapshotAt).TotalMilliseconds < 750)
        {
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _workstationSnapshotQueued, 1) == 1)
        {
            return;
        }

        _lastWorkstationSnapshotAt = now;
        _ = Task.Run(async () =>
        {
            try
            {
                var snapshot = await _workstationHudService.BuildSnapshotAsync(hudInfo, 0, DateTimeOffset.Now, CancellationToken.None);
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is null)
                {
                    return;
                }

                await dispatcher.InvokeAsync(() =>
                {
                    if (_viewModel is null)
                    {
                        return;
                    }

                    _viewModel.WorkStatusText = snapshot.WorkStatusText;
                    _viewModel.FunText = snapshot.FunText;
                    _viewModel.GainedXpText = snapshot.GainedXpText;
                    _viewModel.LevelText = snapshot.LevelText;
                    _viewModel.XpText = snapshot.XpText;
                    _viewModel.XpProgressPercent = snapshot.XpProgressPercent;
                    _viewModel.WorkSummaryText = snapshot.WorkSummaryText;
                    _viewModel.StatsText = snapshot.StatsText;
                    _viewModel.GestureCountText = snapshot.GestureCount.ToString();
                    _viewModel.CopyCountText = snapshot.CopyCount.ToString();
                    _viewModel.PasteCountText = snapshot.PasteCount.ToString();
                    _viewModel.SavedClicksCountText = snapshot.EstimatedSavedClicks.ToString();
                    _viewModel.HudBackgroundBrush = GetHudBackgroundBrush(
                        snapshot.HudThemeKey,
                        snapshot.HudStartColor,
                        snapshot.HudEndColor);
                    _viewModel.HudAccentBrush = GetHudAccentBrush(snapshot.HudAccentColor);
                });
            }
            catch
            {
                // HUD data is auxiliary; gesture drawing must stay smooth if stats cannot load.
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _workstationSnapshotQueued, 0);
            }
        });
    }

    private void PositionWindow()
    {
        if (_window is null)
        {
            return;
        }

        var bounds = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        if (_lastWindowBounds == bounds)
        {
            return;
        }

        _lastWindowBounds = bounds;
        _window.Left = bounds.Left;
        _window.Top = bounds.Top;
        _window.Width = bounds.Width;
        _window.Height = bounds.Height;
    }

    private PointCollection ToPointCollection(IReadOnlyList<GesturePoint> points)
    {
        var collection = new PointCollection(points.Count);
        foreach (var point in points)
        {
            collection.Add(ToOverlayPoint(point));
        }

        collection.Freeze();
        return collection;
    }

    private System.Windows.Point ToOverlayPoint(GesturePoint point)
    {
        if (_window is null)
        {
            return new System.Windows.Point(point.X, point.Y);
        }

        // Hook coordinates are physical screen pixels, while WPF renders in device-independent units.
        // PointFromScreen keeps the trail attached to the cursor under DPI scaling and multi-monitor layouts.
        return _window.PointFromScreen(new System.Windows.Point(point.X, point.Y));
    }

    private static IReadOnlyList<GesturePoint> TrimVisiblePoints(IReadOnlyList<GesturePoint> points)
    {
        if (points.Count <= MaxVisiblePointCount)
        {
            return points;
        }

        return points.Skip(points.Count - MaxVisiblePointCount).ToArray();
    }

    private System.Windows.Media.Brush GetStrokeBrush(string colorText)
    {
        if (_lastStrokeBrush is not null &&
            string.Equals(_lastStrokeColorText, colorText, StringComparison.OrdinalIgnoreCase))
        {
            return _lastStrokeBrush;
        }

        _lastStrokeColorText = colorText;
        _lastStrokeBrush = CreateStrokeBrush(colorText);
        return _lastStrokeBrush;
    }

    private static System.Windows.Media.Brush CreateStrokeBrush(string colorText)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorText)!;
            brush.Freeze();
            return brush;
        }
        catch
        {
            var fallback = new SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 200, 255));
            fallback.Freeze();
            return fallback;
        }
    }

    private System.Windows.Media.Brush GetHudBackgroundBrush(string themeKey, string startColor, string endColor)
    {
        if (_lastHudBackgroundBrush is not null &&
            string.Equals(_lastHudThemeKey, themeKey, StringComparison.OrdinalIgnoreCase))
        {
            return _lastHudBackgroundBrush;
        }

        _lastHudThemeKey = themeKey;
        _lastHudBackgroundBrush = CreateHudGradientBrush(startColor, endColor);
        return _lastHudBackgroundBrush;
    }

    private System.Windows.Media.Brush GetHudAccentBrush(string colorText)
    {
        if (_lastHudAccentBrush is not null &&
            string.Equals(_lastHudAccentColorText, colorText, StringComparison.OrdinalIgnoreCase))
        {
            return _lastHudAccentBrush;
        }

        _lastHudAccentColorText = colorText;
        _lastHudAccentBrush = CreateSolidBrush(colorText, System.Windows.Media.Color.FromRgb(147, 197, 253));
        return _lastHudAccentBrush;
    }

    private static System.Windows.Media.Brush CreateHudGradientBrush(string startColor, string endColor)
    {
        var start = ParseColor(startColor, System.Windows.Media.Color.FromArgb(220, 17, 23, 36));
        var end = ParseColor(endColor, System.Windows.Media.Color.FromArgb(220, 30, 41, 59));
        var brush = new LinearGradientBrush(start, end, 0)
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Brush CreateSolidBrush(string colorText, System.Windows.Media.Color fallbackColor)
    {
        var brush = new SolidColorBrush(ParseColor(colorText, fallbackColor));
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color ParseColor(string colorText, System.Windows.Media.Color fallback)
    {
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorText);
        }
        catch
        {
            return fallback;
        }
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "已下班";
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}小时{value.Minutes:D2}分"
            : $"{Math.Max(0, value.Minutes)}分钟";
    }

    private static string FormatMoney(decimal value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"￥{value:0.00}");
    }

    private static string FormatPaydayCountdown(int days)
    {
        return days <= 0 ? "今天" : $"{days} 天";
    }

    private async Task HideLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(700, cancellationToken);
            await HideAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

}



