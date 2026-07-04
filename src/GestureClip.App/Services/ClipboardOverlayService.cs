using GestureClip.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace GestureClip.App.Services;

public sealed class ClipboardOverlayService : IClipboardOverlayService
{
    private readonly IServiceProvider _serviceProvider;
    private ClipboardOverlayWindow? _window;

    public ClipboardOverlayService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ShowAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var window = EnsureWindow();

            await window.LoadHistoryAsync();
            window.Show();
            window.Activate();
            window.FocusSearchBox();
        });
    }

    public async Task ToggleAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var window = EnsureWindow();
            if (window.IsVisible)
            {
                window.Hide();
                return;
            }

            await window.LoadHistoryAsync();
            window.Show();
            window.Activate();
            window.FocusSearchBox();
        });
    }

    public async Task RefreshAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_window is not null)
            {
                await _window.LoadHistoryAsync();
            }
        });
    }

    private ClipboardOverlayWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = _serviceProvider.GetRequiredService<ClipboardOverlayWindow>();
        _window.Closed += (_, _) => _window = null;
        return _window;
    }
}
