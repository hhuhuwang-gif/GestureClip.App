using GestureClip.Core.Abstractions;

namespace GestureClip.Features.Gestures;

public sealed class MouseGestureDependencies
{
    public required ILowLevelMouseHook MouseHook { get; init; }
    public required IMouseGestureRecognizer Recognizer { get; init; }
    public required IMouseGestureActionExecutor ActionExecutor { get; init; }
    public required IRightClickSynthesizer RightClickSynthesizer { get; init; }
    public required IMouseButtonStateProvider MouseButtonStateProvider { get; init; }
    public required IGestureSettingsProvider SettingsProvider { get; init; }
    public required IGesturePresetProvider PresetProvider { get; init; }
    public required IGestureHudInfoProvider HudInfoProvider { get; init; }
    public required IGestureOverlayService GestureOverlayService { get; init; }
    public required IForegroundAppService ForegroundAppService { get; init; }
    public required IAppBlacklistService AppBlacklistService { get; init; }
    public required IWorkstationDashboardService WorkstationDashboardService { get; init; }
    public required IWorkerLevelService WorkerLevelService { get; init; }
    public required IWorkerLevelUpService WorkerLevelUpService { get; init; }
}
