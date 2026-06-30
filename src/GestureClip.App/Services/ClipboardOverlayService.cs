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
            if (_window is null)
            {
                _window = _serviceProvider.GetRequiredService<ClipboardOverlayWindow>();
                _window.Closed += (_, _) => _window = null;
            }

            await _window.LoadHistoryAsync();
            _window.Show();
            _window.Activate();
            _window.FocusSearchBox();
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
}
