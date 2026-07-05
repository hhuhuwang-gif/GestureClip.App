using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Clipboard;
using GestureClip.Infrastructure.Database;
using GestureClip.Infrastructure.Gestures;
using GestureClip.Infrastructure.Hotkeys;
using GestureClip.Infrastructure.Paths;
using GestureClip.Infrastructure.Settings;
using GestureClip.Infrastructure.Startup;
using GestureClip.Infrastructure.SystemIntegration;
using GestureClip.Infrastructure.SystemInfo;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGestureClipInfrastructure(
        this IServiceCollection services,
        AppPathProvider paths)
    {
        paths.EnsureDirectories();

        services.AddSingleton(paths);
        services.AddSingleton(new DatabaseOptions { DatabasePath = paths.DatabasePath });
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<SqlMigrationRunner>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ISettingsService, SqliteSettingsService>();
        services.AddSingleton<ISystemPermissionService, SystemPermissionService>();
        services.AddSingleton<IAppEnvironment, AppEnvironment>();
        services.AddSingleton<IClipboardListener, WindowsClipboardListener>();
        services.AddSingleton<IClipboardTextReader, WpfClipboardTextReader>();
        services.AddSingleton<IClipboardWriter, WpfClipboardWriter>();
        services.AddSingleton<IForegroundAppService, ForegroundAppService>();
        services.AddSingleton<ICursorPositionProvider, WindowsCursorPositionProvider>();
        services.AddSingleton<ILowLevelMouseHook, LowLevelMouseHook>();
        services.AddSingleton<IRightClickSynthesizer, RightClickSynthesizer>();
        services.AddSingleton<IKeyboardInputSender, KeyboardInputSender>();
        services.AddSingleton<IHotkeyRegistrar, WindowsHotkeyRegistrar>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IStartupRegistry, WindowsStartupRegistry>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        services.AddSingleton<IUrlLauncher, WindowsUrlLauncher>();

        return services;
    }
}
