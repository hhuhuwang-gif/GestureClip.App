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
    public async Task ToggleRestReminderAsync()
    {
        if (_snapshot.RestReminderEnabled)
        {
            await MuteRestReminderTodayAsync();
            return;
        }

        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, true, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderMutedDate, string.Empty, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderSnoozedUntil, string.Empty, CancellationToken.None);
        LastMessage = "休息提醒已开启。";
        await RefreshAsync();
    }

    public async Task SnoozeRestReminderAsync()
    {
        if (_overworkReminderService is not null)
        {
            await _overworkReminderService.SnoozeAsync(DateTimeOffset.Now, CancellationToken.None);
        }
        else if (_settingsService is not null)
        {
            await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderSnoozedUntil, DateTimeOffset.Now.AddMinutes(15).ToString("O"), CancellationToken.None);
        }

        LastMessage = "好，稍后提醒。小熊先退下。";
        await RefreshAsync();
    }

    public async Task MuteRestReminderTodayAsync()
    {
        var now = DateTimeOffset.Now;
        if (_overworkReminderService is not null)
        {
            await _overworkReminderService.MuteTodayAsync(now, CancellationToken.None);
        }
        else if (_settingsService is not null)
        {
            await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderMutedDate, DateOnly.FromDateTime(now.Date).ToString("yyyy-MM-dd"), CancellationToken.None);
        }

        LastMessage = "今天不再打扰你。";
        await RefreshAsync();
    }

    public async Task DisableRestReminderAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, false, CancellationToken.None);
        LastMessage = "休息提醒已关闭，可随时再开。";
        await RefreshAsync();
    }

    public async Task MuteDailyReportTodayAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkBearDailyReportMutedDate, DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"), CancellationToken.None);
        LastMessage = "今日自动报告不再显示。";
        await RefreshAsync();
    }

    public async Task ToggleAutoDailyReportAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        var enabled = !AutoShowDailyReport;
        await _settingsService.SetAsync(SettingKeys.AutoShowDailyWorkReport, enabled, CancellationToken.None);
        LastMessage = enabled ? "下班后自动报告已开启。" : "下班后自动报告已关闭。";
        OnPropertyChanged(nameof(AutoShowDailyReport));
        OnPropertyChanged(nameof(AutoDailyReportButtonText));
    }
}
