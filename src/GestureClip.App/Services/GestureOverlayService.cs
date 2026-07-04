using System.Windows;
using System.Globalization;
using System.Windows.Media;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.Services;

public sealed class GestureOverlayService : IGestureOverlayService
{
    private const int MaxVisiblePointCount = 96;

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
    private readonly IWorkstationHudService _workstationHudService;
    private DateTimeOffset _lastWorkstationSnapshotAt = DateTimeOffset.MinValue;
    private int _workstationSnapshotQueued;
    private Rect _lastWindowBounds = Rect.Empty;
    private string? _lastStrokeColorText;
    private System.Windows.Media.Brush? _lastStrokeBrush;

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
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            EnsureWindow();
            PositionWindow();
            _hideCts?.Cancel();
            ApplyHudInfo(hudInfo);
            _viewModel!.Points = ToPointCollection([point]);
            _window!.Show();
        });
    }

    public async Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        points = TrimVisiblePoints(points);

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastUpdateAt).TotalMilliseconds < 33)
        {
            StorePendingUpdate(points, hudInfo);
            return;
        }

        _lastUpdateAt = now;
        lock (_updateSyncRoot)
        {
            _pendingUpdatePoints = points;
            _pendingUpdateHudInfo = hudInfo;
            if (_updateQueued)
            {
                return;
            }

            _updateQueued = true;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => FlushPendingUpdate(cancellationToken));
    }

    public async Task CompleteGestureAsync(GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            EnsureWindow();
            ApplyHudInfo(hudInfo);
            _window!.Show();
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            _ = HideLaterAsync(_hideCts.Token);
        });
    }

    public async Task ShowPatternAsync(string pattern, CancellationToken cancellationToken)
    {
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
            _window!.Show();
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            _ = HideLaterAsync(_hideCts.Token);
        });
    }

    public async Task HideAsync(CancellationToken cancellationToken)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearPendingUpdate();
            _hideCts?.Cancel();
            _window?.Hide();
        });
    }

    private void StorePendingUpdate(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo)
    {
        lock (_updateSyncRoot)
        {
            _pendingUpdatePoints = points;
            _pendingUpdateHudInfo = hudInfo;
        }
    }

    private void FlushPendingUpdate(CancellationToken cancellationToken)
    {
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

        if (points is null || hudInfo is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindow();
        PositionWindow();
        ApplyHudInfo(hudInfo);
        _viewModel!.Points = ToPointCollection(points);
        _window!.Show();
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
        _window.Closed += (_, _) =>
        {
            _window = null;
            _viewModel = null;
        };
    }

    private void ApplyHudInfo(GestureHudInfo hudInfo)
    {
        _viewModel!.DirectionText = hudInfo.DirectionText;
        _viewModel.Pattern = hudInfo.Pattern;
        _viewModel.ActionName = hudInfo.ActionName;
        _viewModel.ShortcutText = hudInfo.ShortcutText;
        _viewModel.PresetName = hudInfo.PresetName;
        _viewModel.StrokeBrush = GetStrokeBrush(_settingsService.Get(SettingKeys.GestureStrokeColor, "#8CC8FF"));
        QueueWorkstationSnapshotRefresh(hudInfo);
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

        return collection;
    }

    private System.Windows.Point ToOverlayPoint(GesturePoint point)
    {
        if (_window is null)
        {
            return new System.Windows.Point(point.X, point.Y);
        }

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
            await Task.Delay(800, cancellationToken);
            await HideAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

}



