namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Gestures;

public interface IGestureOverlayService
{
    Task ShowGestureStartAsync(GesturePoint point, GestureHudInfo hudInfo, CancellationToken cancellationToken);

    Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken);

    Task CompleteGestureAsync(GestureHudInfo hudInfo, CancellationToken cancellationToken);

    Task ShowPatternAsync(string pattern, CancellationToken cancellationToken);

    Task HideAsync(CancellationToken cancellationToken);
}
