using System.Globalization;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using GestureClip.Features.WorkerLevel;

namespace GestureClip.Features.Workstation;

public sealed class WorkstationHudService : IWorkstationHudService
{
    private readonly ISettingsService _settingsService;
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly IWorkerLevelService _workerLevelService;

    public WorkstationHudService(
        ISettingsService settingsService,
        IWorkstationDashboardService dashboardService,
        IWorkerLevelService workerLevelService)
    {
        _settingsService = settingsService;
        _dashboardService = dashboardService;
        _workerLevelService = workerLevelService;
    }

    public async Task<WorkstationHudSnapshot> BuildSnapshotAsync(
        GestureHudInfo hudInfo,
        int gainedXp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetSnapshotAsync(now, cancellationToken);
        var level = await _workerLevelService.GetSnapshotAsync(cancellationToken);
        var showFun = _settingsService.Get(SettingKeys.HudFunTextEnabled, true);
        var showLevel = _settingsService.Get(SettingKeys.HudStatusLevelEnabled, true) &&
            _settingsService.Get(SettingKeys.WorkerLevelShowLevelInHud, true);

        var displayXp = gainedXp > 0 ? gainedXp : EstimateGestureXp(hudInfo.Action);

        return new WorkstationHudSnapshot(
            showFun ? GetFunText(hudInfo.Action) : string.Empty,
            displayXp > 0 ? $"经验 +{displayXp}" : string.Empty,
            showLevel ? level.LevelText : string.Empty,
            showLevel ? level.XpText : string.Empty,
            showLevel ? level.ProgressPercent : 0d,
            $"今日 {FormatMoney(dashboard.TodayEarned)} · 下班 {FormatDuration(dashboard.TimeUntilOffWork)} · 发薪 {FormatPayday(dashboard.DaysUntilPayday)}",
            $"手势 {dashboard.GestureCount} · 复制 {dashboard.CopyCount} · 粘贴 {dashboard.PasteCount} · 少点 {dashboard.EstimatedSavedClicks} 次",
            dashboard.WorkStatusText,
            showLevel);
    }

    private static int EstimateGestureXp(BuiltInGestureAction action)
    {
        return action == BuiltInGestureAction.None
            ? 0
            : WorkerLevelService.GetActionXp(action) + 2;
    }
    private static string GetFunText(BuiltInGestureAction action)
    {
        var reports = action switch
        {
            BuiltInGestureAction.Copy => new[]
            {
                "复制成功，知识正在搬砖",
                "Ctrl+C 已打入灵魂",
                "已复制，打工素材已入库"
            },
            BuiltInGestureAction.Paste => new[]
            {
                "粘贴成功，牛马效率 +1",
                "Ctrl+V 成功，复用才是生产力",
                "已粘贴，拒绝重复造轮子"
            },
            BuiltInGestureAction.Enter => new[]
            {
                "回车确认，命运提交成功",
                "Enter 已按下，无法撤回的人生",
                "确认完成，需求已被推进"
            },
            BuiltInGestureAction.Escape => new[]
            {
                "Esc 成功，及时跑路也是智慧",
                "撤退成功，牛马保命 +1",
                "取消完成，逃过一劫"
            },
            BuiltInGestureAction.Undo => new[]
            {
                "撤销成功，人生还有后悔药",
                "Ctrl+Z，时间回溯一点点",
                "撤销完成，工位时间倒流"
            },
            BuiltInGestureAction.SelectAll => new[]
            {
                "全选完成，材料一锅端走",
                "Ctrl+A 成功，整页打包带走"
            },
            BuiltInGestureAction.SendAltLeft => new[]
            {
                "后退成功，先撤一步",
                "Alt+← 成功，回到上一幕"
            },
            BuiltInGestureAction.SendAltRight => new[]
            {
                "前进成功，继续推进",
                "Alt+→ 成功，剧情继续"
            },
            BuiltInGestureAction.OpenClipboardOverlay => new[]
            {
                "剪贴板已打开，素材库上线",
                "历史面板已召唤，打工材料集合"
            },
            BuiltInGestureAction.PasteLatestClipboardItem => new[]
            {
                "最近一条已粘贴，复用才是生产力",
                "历史粘贴完成，少打一遍是一遍"
            },
            BuiltInGestureAction.PasteAndEnter => new[]
            {
                "粘贴并确认，一套连招完成",
                "Ctrl+V + Enter，打工连击 +1"
            },
            BuiltInGestureAction.None => new[] { "这个手势还没绑定动作" },
            _ => new[]
            {
                "动作完成，工位效率 +1",
                "手势生效，鼠标已被驯服"
            }
        };

        return reports[Random.Shared.Next(reports.Length)];
    }

    private static string FormatMoney(decimal value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"￥{value:0.00}");
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "已下班";
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}小时{value.Minutes:D2}分"
            : $"{Math.Max(0, value.Minutes)}分钟";
    }

    private static string FormatPayday(int days)
    {
        return days <= 0 ? "今天" : $"{days} 天";
    }
}


