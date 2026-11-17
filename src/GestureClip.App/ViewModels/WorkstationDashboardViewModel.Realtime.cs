using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.App.ViewModels;

using Media = System.Windows.Media;

public sealed partial class WorkstationDashboardViewModel : INotifyPropertyChanged
{
    // Live extrapolation between snapshots: earnings/countdown tick every second
    // without touching the database. The 5s full refresh corrects any drift.
    private DateTimeOffset _snapshotAt = DateTimeOffset.Now;

    private TimeSpan ElapsedSinceSnapshot
    {
        get
        {
            var elapsed = _timeProvider.GetLocalNow() - _snapshotAt;
            return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
        }
    }

    private bool IsEarningNow => _snapshot.WorkTimeStage
        is WorkTimeStage.EarlyWork or WorkTimeStage.MidWork or WorkTimeStage.LateWork;

    private decimal ExtrapolatedTodayEarned => IsEarningNow && _snapshot.MinuteValue > 0
        ? _snapshot.TodayEarned + (decimal)ElapsedSinceSnapshot.TotalMinutes * _snapshot.MinuteValue
        : _snapshot.TodayEarned;

    private TimeSpan ExtrapolatedTimeUntilOffWork => _snapshot.TimeUntilOffWork > TimeSpan.Zero
        ? _snapshot.TimeUntilOffWork - TimeSpan.FromSeconds(Math.Floor(ElapsedSinceSnapshot.TotalSeconds))
        : _snapshot.TimeUntilOffWork;

    /// <summary>Raises only the per-second live metrics; called by the window's 1s timer.</summary>
    public void TickRealtime()
    {
        _snapshotNotifications[nameof(TodayEarnedText)]();
        _snapshotNotifications[nameof(BossPaidText)]();
        _snapshotNotifications[nameof(OffWorkCountdownText)]();
        _snapshotNotifications[nameof(OffWorkProgressText)]();
    }

    public IReadOnlyList<string> HolidayCountdownTexts =>
        GetHolidayCountdowns(DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date));
}
