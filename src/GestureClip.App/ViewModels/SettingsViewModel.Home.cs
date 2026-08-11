using System.Threading;
using GestureClip.Core.Abstractions;

namespace GestureClip.App.ViewModels;

// Home tab: lightweight live stats for the Settings landing page.
public sealed partial class SettingsViewModel
{
    private string _todayStatsText = "今天还没有动作";
    private string _todayEarnedText = "￥0.00";
    private bool _todayStatsRefreshing;

    public string TodayStatsText
    {
        get => _todayStatsText;
        private set
        {
            if (_todayStatsText == value)
            {
                return;
            }

            _todayStatsText = value;
            OnPropertyChanged();
        }
    }

    public string TodayEarnedText
    {
        get => _todayEarnedText;
        private set
        {
            if (_todayEarnedText == value)
            {
                return;
            }

            _todayEarnedText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Live today summary for the home page. Never blocks or throws.</summary>
    public async Task RefreshTodayStatsAsync()
    {
        if (_workstationDashboardService is null || _todayStatsRefreshing)
        {
            return;
        }

        _todayStatsRefreshing = true;
        try
        {
            var snapshot = await _workstationDashboardService.GetSnapshotAsync(DateTimeOffset.Now, CancellationToken.None);
            TodayStatsText = $"复制 {snapshot.CopyCount}  ·  粘贴 {snapshot.PasteCount}  ·  手势 {snapshot.GestureCount}  ·  打开剪贴板 {snapshot.OpenClipboardCount} 次";
            TodayEarnedText = snapshot.TodayEarned.ToString("￥0.00");
        }
        catch
        {
            // Optional local stat; the home page must never fail because of it.
        }
        finally
        {
            _todayStatsRefreshing = false;
        }
    }
}
