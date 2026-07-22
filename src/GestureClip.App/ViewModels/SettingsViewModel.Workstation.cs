using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Diagnostics;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Core.Gestures;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Workstation;
using GestureClip.Features.Gestures;
using GestureClip.Features.Workstation;
using GestureClip.App.Services;
using GestureClip.Infrastructure.Paths;
using System.Windows.Data;
using System.Windows.Threading;

namespace GestureClip.App.ViewModels;

public sealed partial class SettingsViewModel
{

    private async Task RefreshWorkerLevelAsync()
    {
        try
        {
            var snapshot = await _workerLevelService.GetSnapshotAsync(CancellationToken.None);
            WorkerLevelText = snapshot.LevelText;
            WorkerXpText = snapshot.XpText;
        }
        catch
        {
            WorkerLevelText = "Lv.1 初入工位";
            WorkerXpText = "XP 0 / 50";
        }
    }


    private void SetWorkstationTimeSetting(ref string field, string value, string key, [CallerMemberName] string? propertyName = null)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "00:00" : value.Trim();
        if (field == normalized)
        {
            return;
        }

        field = normalized;
        OnPropertyChanged(propertyName);
        NotifyWorkstationPreviewChanged();
        _ = _settingsService.SetAsync(key, normalized, CancellationToken.None);
    }


    private void ApplyWorkstationTemplate(object? parameter)
    {
        if (parameter is not WorkstationTemplateOption template)
        {
            return;
        }

        WorkstationWorkStartTime = template.WorkStartTime;
        WorkstationWorkEndTime = template.WorkEndTime;
        WorkstationLunchStartTime = template.LunchStartTime;
        WorkstationLunchEndTime = template.LunchEndTime;
        WorkstationWorkdays = template.Workdays;
    }


    private decimal EstimateTodayEarned(DateTime now)
    {
        if (WorkstationMonthlySalary <= 0m ||
            !IsWorkday(now.DayOfWeek) ||
            !TryParseTime(WorkstationWorkStartTime, out var start) ||
            !TryParseTime(WorkstationWorkEndTime, out var end))
        {
            return 0m;
        }

        var startAt = now.Date.Add(start);
        var endAt = now.Date.Add(end);
        if (endAt <= startAt || now <= startAt)
        {
            return 0m;
        }

        var worked = Math.Min((now - startAt).TotalMinutes, (endAt - startAt).TotalMinutes);
        var total = Math.Max(1, (endAt - startAt).TotalMinutes);
        var dailySalary = WorkstationMonthlySalary / 21.75m;
        return dailySalary * (decimal)(worked / total);
    }


    private string GetWorkstationStatusText(DateTime now)
    {
        if (!IsWorkday(now.DayOfWeek))
        {
            return "休息日，别想工作";
        }

        if (!TryParseTime(WorkstationWorkStartTime, out var start) ||
            !TryParseTime(WorkstationWorkEndTime, out var end) ||
            !TryParseTime(WorkstationLunchStartTime, out var lunchStart) ||
            !TryParseTime(WorkstationLunchEndTime, out var lunchEnd))
        {
            return "时间配置需要检查";
        }

        var time = now.TimeOfDay;
        if (time < start)
        {
            return "还没上班";
        }

        if (time >= end)
        {
            return "已下班";
        }

        if (lunchEnd > lunchStart && time >= lunchStart && time < lunchEnd)
        {
            return "午休中";
        }

        return (end - time).TotalMinutes <= 30 ? "即将下班" : "上班中";
    }


    private void NotifyWorkstationPreviewChanged()
    {
        OnPropertyChanged(nameof(WorkstationPreviewStatusText));
        OnPropertyChanged(nameof(WorkstationPreviewTodayEarnedText));
        OnPropertyChanged(nameof(WorkstationPreviewOffWorkText));
        OnPropertyChanged(nameof(WorkstationPreviewPaydayText));
        OnPropertyChanged(nameof(WorkstationPreviewSummaryText));
        NotifyOverworkPreviewChanged();
    }


    private void NotifyOverworkPreviewChanged()
    {
        OnPropertyChanged(nameof(OverworkPreviewStageText));
        OnPropertyChanged(nameof(OverworkPreviewHudColorText));
        OnPropertyChanged(nameof(OverworkPreviewNextReminderText));
        OnPropertyChanged(nameof(OverworkPreviewWorkedText));
    }


    private WorkTimeStageSnapshot GetOverworkPreviewSnapshot(DateTime now)
    {
        var service = new WorkTimeStageService(_settingsService);
        return service.GetSnapshot(now);
    }

}
