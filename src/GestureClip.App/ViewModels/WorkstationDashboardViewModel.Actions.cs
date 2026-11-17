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
    public async Task StartFishingAsync()
    {
        await _dashboardService.StartFishingAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = "摸鱼计时开始了，记得回来点结束。";
        await RefreshAsync();
    }

    public async Task EndFishingAsync()
    {
        await _dashboardService.EndFishingAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = "本次摸鱼已记下，不会上传。";
        await RefreshAsync();
    }

    public Task ToggleFishingAsync() => _snapshot.IsFishing ? EndFishingAsync() : StartFishingAsync();

    public async Task ClearFishingAsync()
    {
        await _dashboardService.ClearTodayFishingAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        LastMessage = "今日摸鱼记录已清空。";
        await RefreshAsync();
    }

    public async Task ResetTodayAsync()
    {
        if (_confirmationService is not null)
        {
            var confirmed = _confirmationService.Confirm(
                "重置今日统计",
                "将清空今天的摸鱼计时、复制/粘贴/手势次数等本地统计。\n此操作不可撤销，确定继续？");
            if (!confirmed)
            {
                LastMessage = "已取消重置。";
                return;
            }
        }

        await _dashboardService.ResetTodayAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        LastMessage = "今日统计已重置，可以从零开始。";
        await RefreshAsync();
    }

    public async Task GenerateReportAsync()
    {
        var report = await _dashboardService.GenerateDailyReportAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = report.ReportText;
        await RefreshAsync();
    }

    public async Task GenerateShareCardAsync()
    {
        if (_shareCardService is null)
        {
            LastMessage = "分享卡片服务未加载。";
            return;
        }

        if (_settingsService?.Get(SettingKeys.EnableWorkBearShareCard, true) == false)
        {
            LastMessage = "分享卡片功能已关闭。";
            return;
        }

        var path = await _shareCardService.GenerateTodayCardAsync(ShareCardStyle, CancellationToken.None);
        _lastShareCardPath = path;
        LastMessage = $"分享卡片已生成（{ShareCardStyle}，不含剪贴板内容）：{path}";
        _shareCardService.OpenCardFolder(path);
    }

    public async Task GeneratePeriodReportAsync(int dayCount)
    {
        PeriodReportText = await _dashboardService.GeneratePeriodReportAsync(DateTimeOffset.Now, dayCount, CancellationToken.None);
        PeriodExpanded = true;
        LastMessage = dayCount <= 7 ? "本周总结已生成（仅本地）。" : "近 30 天总结已生成（仅本地）。";
    }

    public Task OpenLastShareCardFolderAsync()
    {
        if (_shareCardService is null || string.IsNullOrWhiteSpace(_lastShareCardPath))
        {
            LastMessage = "还没有生成过分享卡片。";
            return Task.CompletedTask;
        }

        _shareCardService.OpenCardFolder(_lastShareCardPath);
        LastMessage = "已打开卡片所在文件夹。";
        return Task.CompletedTask;
    }

    public async Task SetSprintAsync(bool enabled)
    {
        await _dashboardService.SetSprintModeAsync(enabled, CancellationToken.None);
        LastMessage = enabled ? "下班冲刺开始，建议进入低功耗模式。" : "下班冲刺已关闭。";
        await RefreshAsync();
    }
}
