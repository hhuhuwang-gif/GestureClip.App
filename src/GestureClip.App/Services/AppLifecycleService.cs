using System.Windows;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.App.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppLifecycleService> _logger;
    private SettingsWindow? _settingsWindow;
    private int _exitStarted;

    public AppLifecycleService(IServiceProvider serviceProvider, ILogger<AppLifecycleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsExplicitExit { get; private set; }

    public bool RuntimeStopped { get; private set; }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
        _logger.LogInformation("Settings window shown.");
    }

    public async void ExitApplication()
    {
        if (Interlocked.Exchange(ref _exitStarted, 1) == 1)
        {
            return;
        }

        IsExplicitExit = true;
        _logger.LogInformation("Explicit application exit requested.");
        await StopRuntimeAsync();
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    public async Task StopRuntimeAsync()
    {
        if (RuntimeStopped)
        {
            return;
        }

        RuntimeStopped = true;
        await StopStepAsync("global hotkey", () =>
        {
            _serviceProvider.GetService<IGlobalHotkeyService>()?.Stop();
            return Task.CompletedTask;
        });
        await StopStepAsync("edge trigger service", () =>
            _serviceProvider.GetService<IEdgeTriggerService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("mouse gesture service", () =>
            _serviceProvider.GetService<IMouseGestureService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("clipboard service", () =>
            _serviceProvider.GetService<IClipboardService>()?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
        await StopStepAsync("tray icon", () =>
        {
            _serviceProvider.GetService<TrayIconService>()?.Dispose();
            return Task.CompletedTask;
        });
    }

    private async Task StopStepAsync(string name, Func<Task> stopAsync)
    {
        try
        {
            await stopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            _logger.LogInformation("Stopped {RuntimeComponent}.", name);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timed out while stopping {RuntimeComponent}; continuing shutdown.", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop {RuntimeComponent}; continuing shutdown.", name);
        }
    }
}

