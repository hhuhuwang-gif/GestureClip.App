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

    public AppLifecycleService(IServiceProvider serviceProvider, ILogger<AppLifecycleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsExplicitExit { get; private set; }

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

    public void ExitApplication()
    {
        IsExplicitExit = true;
        _logger.LogInformation("Explicit application exit requested.");
        System.Windows.Application.Current.Shutdown();
    }
}

