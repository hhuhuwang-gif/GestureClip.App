using GestureClip.Core.Abstractions;
using GestureClip.Features.Clipboard;
using GestureClip.Features.Gestures;
using GestureClip.Features.Diagnostics;
using GestureClip.Features.Privacy;
using GestureClip.Features.Runtime;
using GestureClip.Features.Startup;
using GestureClip.Features.Workstation;
using GestureClip.Features.WorkerLevel;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.Features.DependencyInjection;

public static class FeaturesServiceCollectionExtensions
{
    public static IServiceCollection AddGestureClipFeatures(this IServiceCollection services)
    {
        services.AddSingleton<DefaultDataSeeder>();
        services.AddSingleton<IClipboardHashService, ClipboardHashService>();
        services.AddSingleton<ISensitiveContentDetector, SensitiveContentDetector>();
        services.AddSingleton<IClipboardRepository, ClipboardRepository>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IAppBlacklistService, AppBlacklistService>();
        services.AddSingleton<IMouseGestureRecognizer, DirectionGestureRecognizer>();
        services.AddSingleton<IGestureSettingsProvider, GestureSettingsProvider>();
        services.AddSingleton<IGesturePresetProvider, GesturePresetProvider>();
        services.AddSingleton<IGestureHudInfoProvider, GestureHudInfoProvider>();
        services.AddSingleton<IMouseGestureActionExecutor, GestureBuiltInActionExecutor>();
        services.AddSingleton<IMouseGestureService, MouseGestureService>();
        services.AddSingleton<IEdgeTriggerService, EdgeTriggerService>();
        services.AddSingleton<IFeatureToggleService, FeatureToggleService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IWorkstationStatsRepository, WorkstationStatsRepository>();
        services.AddSingleton<IWorkstationDashboardService, WorkstationDashboardService>();
        services.AddSingleton<IWorkTimeStageService, WorkTimeStageService>();
        services.AddSingleton<IWorkerLevelService, WorkerLevelService>();
        services.AddSingleton<IWorkstationHudService, WorkstationHudService>();
        services.AddSingleton<IOverworkReminderService, OverworkReminderService>();
        return services;
    }
}

