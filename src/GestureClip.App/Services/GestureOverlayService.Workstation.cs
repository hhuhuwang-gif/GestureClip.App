using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GestureClip.Core.Gestures;
using GestureClip.Core.Workstation;

namespace GestureClip.App.Services;

public sealed partial class GestureOverlayService
{
    private void QueueWorkstationSnapshotRefresh(GestureHudInfo hudInfo)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastWorkstationSnapshotAt).TotalMilliseconds < 750)
        {
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _workstationSnapshotQueued, 1) == 1)
        {
            return;
        }

        _lastWorkstationSnapshotAt = now;
        _ = Task.Run(async () =>
        {
            try
            {
                var snapshot = await _workstationHudService.BuildSnapshotAsync(hudInfo, 0, DateTimeOffset.Now, CancellationToken.None);
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is null)
                {
                    return;
                }

                await dispatcher.InvokeAsync(() => ApplyWorkstationSnapshot(snapshot));
            }
            catch (Exception ex)
            {
                // HUD data is auxiliary; gesture drawing must stay smooth if stats cannot load.
                System.Diagnostics.Trace.TraceError("Gesture HUD snapshot failed: {0}", ex);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _workstationSnapshotQueued, 0);
            }
        });
    }

    private void ApplyWorkstationSnapshot(WorkstationHudSnapshot snapshot)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.WorkStatusText = snapshot.WorkStatusText;
        _viewModel.FunText = snapshot.FunText;
        _viewModel.GainedXpText = snapshot.GainedXpText;
        _viewModel.LevelText = snapshot.LevelText;
        _viewModel.XpText = snapshot.XpText;
        _viewModel.XpProgressPercent = snapshot.XpProgressPercent;
        _viewModel.WorkSummaryText = snapshot.WorkSummaryText;
        _viewModel.StatsText = snapshot.StatsText;
        _viewModel.GestureCountText = snapshot.GestureCount.ToString();
        _viewModel.CopyCountText = snapshot.CopyCount.ToString();
        _viewModel.PasteCountText = snapshot.PasteCount.ToString();
        _viewModel.SavedClicksCountText = snapshot.EstimatedSavedClicks.ToString();
        _viewModel.HudBackgroundBrush = GetHudBackgroundBrush(
            snapshot.HudThemeKey,
            snapshot.HudStartColor,
            snapshot.HudEndColor);
        _viewModel.HudAccentBrush = GetHudAccentBrush(snapshot.HudAccentColor);

    }
}
