using System.Windows;
using System.Reflection;
using GestureClip.App.DependencyInjection;
using GestureClip.App.Services;
using GestureClip.Core.Abstractions;
using GestureClip.Features.Startup;
using GestureClip.Infrastructure.Database;
using GestureClip.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private SingleInstanceService? _singleInstanceService;
    private TrayIconService? _trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var exitAfterStartup = e.Args.Any(
            arg => string.Equals(arg, "--smoke-exit-after-startup", StringComparison.OrdinalIgnoreCase));
        _singleInstanceService = new SingleInstanceService();
        _singleInstanceService.ActivationRequested += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                _serviceProvider?.GetService<AppLifecycleService>()?.ToggleSettingsWindow();
            });
        };

        if (!_singleInstanceService.TryAcquire())
        {
            _singleInstanceService.SignalExistingInstance();
            System.Windows.Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        var services = new ServiceCollection();
        var paths = new AppPathProvider();
        services.AddGestureClipApp(paths);

        _serviceProvider = services.BuildServiceProvider();
        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();

        try
        {
            var appVersion = typeof(App).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown";
            logger.LogInformation("GestureClip v{AppVersion} starting.", appVersion);

            var initializer = _serviceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync(CancellationToken.None);

            var connectionFactory = _serviceProvider.GetRequiredService<ISqliteConnectionFactory>();
            await using (var connection = await connectionFactory.OpenConnectionAsync(CancellationToken.None))
            {
                var seeder = _serviceProvider.GetRequiredService<DefaultDataSeeder>();
                await seeder.SeedAsync(connection, CancellationToken.None);
            }

            await _serviceProvider.GetRequiredService<IAppBlacklistService>().RefreshAsync(CancellationToken.None);

            MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            _trayIconService = _serviceProvider.GetRequiredService<TrayIconService>();
            _trayIconService.Initialize();

            await _serviceProvider.GetRequiredService<IClipboardService>().StartAsync(CancellationToken.None);
            try
            {
                await _serviceProvider.GetRequiredService<IMouseGestureService>().StartAsync(CancellationToken.None);
            }
            catch (Exception gestureEx)
            {
                logger.LogError(gestureEx, "Mouse gesture service failed to start.");
                System.Windows.MessageBox.Show(
                    "鼠标手势启动失败，应用将继续运行。请查看日志获取详细信息。",
                    "GestureClip",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            await _serviceProvider.GetRequiredService<IEdgeTriggerService>().StartAsync(CancellationToken.None);
            _serviceProvider.GetRequiredService<IGlobalHotkeyService>().Start();
            await _serviceProvider.GetRequiredService<IOverworkReminderService>().StartAsync(CancellationToken.None);

            _serviceProvider.GetRequiredService<AppLifecycleService>().ShowSettingsWindow();
            logger.LogInformation("GestureClip v{AppVersion} started.", appVersion);
            if (exitAfterStartup)
            {
                logger.LogInformation("Smoke exit requested after startup.");
                _serviceProvider.GetRequiredService<AppLifecycleService>().ExitApplication();
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "GestureClip failed during startup.");
            System.Windows.MessageBox.Show(
                "GestureClip 启动失败，请查看日志目录获取详细信息。",
                "GestureClip",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var lifecycle = _serviceProvider?.GetService<AppLifecycleService>();
        if (lifecycle?.RuntimeStopped != true)
        {
            try
            {
                lifecycle?.StopRuntimeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _serviceProvider?.GetService<ILogger<App>>()?.LogError(ex, "Failed while stopping runtime during application exit.");
            }
        }

        _serviceProvider?.GetService<ILogger<App>>()?.LogInformation("GestureClip exiting.");
        _serviceProvider?.Dispose();
        _singleInstanceService?.Dispose();
        base.OnExit(e);
    }
}

