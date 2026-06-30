using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Features.DependencyInjection;
using GestureClip.Infrastructure.DependencyInjection;
using GestureClip.Infrastructure.Logging;
using GestureClip.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.DependencyInjection;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddGestureClipApp(this IServiceCollection services, AppPathProvider paths)
    {
        services.AddGestureClipLogging(paths);
        services.AddGestureClipInfrastructure(paths);
        services.AddGestureClipFeatures();

        services.AddSingleton<AppLifecycleService>();
        services.AddSingleton<IAppLifecycleService>(provider => provider.GetRequiredService<AppLifecycleService>());
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ClipboardOverlayService>();
        services.AddSingleton<IClipboardOverlayService>(provider => provider.GetRequiredService<ClipboardOverlayService>());
        services.AddSingleton<GestureOverlayService>();
        services.AddSingleton<IGestureOverlayService>(provider => provider.GetRequiredService<GestureOverlayService>());
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ClipboardOverlayViewModel>();
        services.AddTransient<GestureOverlayViewModel>();
        services.AddTransient<ClipboardOverlayWindow>();
        services.AddTransient<GestureOverlayWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
