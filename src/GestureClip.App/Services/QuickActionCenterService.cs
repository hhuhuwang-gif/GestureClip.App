using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WpfApp = System.Windows.Application;

namespace GestureClip.App.Services;

public sealed class QuickActionCenterService : IQuickActionCenterService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<QuickActionCenterService> _logger;
    private QuickActionCenterWindow? _window;

    public QuickActionCenterService(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        ILogger<QuickActionCenterService> logger)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task ShowAsync()
    {
        await WpfApp.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = EnsureWindow();
            window.PrepareForShow(GetHotkeyHint());
            window.Show();
            window.Activate();
        });
    }

    public async Task ToggleAsync()
    {
        await WpfApp.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = EnsureWindow();
            if (window.IsVisible)
            {
                window.Hide();
                return;
            }

            window.PrepareForShow(GetHotkeyHint());
            window.Show();
            window.Activate();
        });
    }

    private string GetHotkeyHint()
    {
        var configured = _settingsService.Get(
            SettingKeys.HotkeyOpenQuickActionCenterKey,
            HotkeyDefinition.DefaultOpenQuickActionCenter);
        return HotkeyDefinition.TryParse(configured, out var hotkey)
            ? hotkey.DisplayText
            : HotkeyDefinition.DefaultOpenQuickActionCenter;
    }

    public async Task HideAsync()
    {
        await WpfApp.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_window is not null && _window.IsVisible)
            {
                _window.Hide();
            }
        });
    }

    private QuickActionCenterWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = _serviceProvider.GetRequiredService<QuickActionCenterWindow>();
        _window.Closed += (_, _) =>
        {
            _window = null;
            _logger.LogDebug("Quick action center window closed.");
        };
        return _window;
    }
}
