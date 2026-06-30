using System.Windows;
using System.Windows.Media;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.Services;

public sealed class GestureOverlayService : IGestureOverlayService
{
    private readonly IServiceProvider _serviceProvider;
    private GestureOverlayWindow? _window;
    private GestureOverlayViewModel? _viewModel;
    private CancellationTokenSource? _hideCts;
    private DateTimeOffset _lastUpdateAt = DateTimeOffset.MinValue;

    public GestureOverlayService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ShowGestureStartAsync(GesturePoint point, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastUpdateAt).TotalMilliseconds < 16)
        {
            return;
        }

        _lastUpdateAt = now;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWindow();
            PositionWindow();
            ApplyHudInfo(hudInfo);
            _viewModel!.Points = ToPointCollection(points);
            _window!.Show();
        });
    }

    public async Task CompleteGestureAsync(GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            _hideCts?.Cancel();
            _window?.Hide();
        });
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
    }

    private void PositionWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Left = SystemParameters.VirtualScreenLeft;
        _window.Top = SystemParameters.VirtualScreenTop;
        _window.Width = SystemParameters.VirtualScreenWidth;
        _window.Height = SystemParameters.VirtualScreenHeight;
    }

    private static PointCollection ToPointCollection(IReadOnlyList<GesturePoint> points)
    {
        var collection = new PointCollection(points.Count);
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        foreach (var point in points)
        {
            collection.Add(new System.Windows.Point(point.X - left, point.Y - top));
        }

        return collection;
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
