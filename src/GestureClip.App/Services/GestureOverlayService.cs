using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.Services;

public sealed partial class GestureOverlayService : IGestureOverlayService
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
        var generation = BeginPresentation();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }
            EnsureWindow();
            PositionWindow();
            _hideCts?.Cancel();
            ApplyHudTimePalette(DateTimeOffset.Now);
            ApplyHudInfo(hudInfo, refreshStyle: true);
            _viewModel!.Points = ToPointCollection([point]);
            ShowWindow();
        });
    }

    public Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        points = TrimVisiblePoints(points);
        int generation;
        lock (_updateSyncRoot)
        {
            generation = _generation;
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
        var generation = BeginPresentation();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }
            EnsureWindow();
            ApplyHudTimePalette(DateTimeOffset.Now);
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
        var generation = BeginPresentation();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }
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
        var generation = BeginPresentation();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }
            _hideCts?.Cancel();
            if (_window is not null)
            {
                _window.IsHitTestVisible = false;
                _window.Focusable = false;
            }

            FadeOutAndHideWindow();
        });
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



