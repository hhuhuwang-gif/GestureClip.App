using System.Globalization;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Core.WorkerLevel;
using GestureClip.Core.Workstation;
using GestureClip.Features.WorkerLevel;

namespace GestureClip.Features.Workstation;

public sealed class WorkstationHudService : IWorkstationHudService
{
    private static readonly TimeSpan SnapshotCacheDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ActionMergeWindow = TimeSpan.FromMilliseconds(1500);

    private readonly ISettingsService _settingsService;
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly IWorkerLevelService _workerLevelService;
    private readonly IWorkTimeStageService _workTimeStageService;
    private readonly SemaphoreSlim _snapshotCacheLock = new(1, 1);

    private DateTimeOffset? _snapshotCacheTimestamp;
    private WorkstationDashboardSnapshot? _cachedDashboard;
    private WorkerLevelSnapshot? _cachedLevel;
    private readonly object _mergeLock = new();
    private BuiltInGestureAction _lastMergedAction = BuiltInGestureAction.None;
    private DateTimeOffset _lastMergedAt = DateTimeOffset.MinValue;
    private int _mergedActionCount;

    public WorkstationHudService(
        ISettingsService settingsService,
        IWorkstationDashboardService dashboardService,
        IWorkerLevelService workerLevelService,
        IWorkTimeStageService workTimeStageService)
    {
        _settingsService = settingsService;
        _dashboardService = dashboardService;
        _workerLevelService = workerLevelService;
        _workTimeStageService = workTimeStageService;
    }

    public async Task<WorkstationHudSnapshot> BuildSnapshotAsync(
        GestureHudInfo hudInfo,
        int gainedXp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (dashboard, level) = await GetCachedSnapshotsAsync(now, cancellationToken);
        var showFun = _settingsService.Get(SettingKeys.HudFunTextEnabled, true);
        var showLevel = _settingsService.Get(SettingKeys.HudStatusLevelEnabled, true) &&
            _settingsService.Get(SettingKeys.WorkerLevelShowLevelInHud, true);
        var stageSnapshot = _workTimeStageService.GetSnapshot(now);
        var showStatusText = _settingsService.Get(SettingKeys.EnableWorkBearHudStatusText, true);
        var enableTimeColor = _settingsService.Get(
            SettingKeys.EnableWorkBearHudThemeColor,
            _settingsService.Get(SettingKeys.WorkstationEnableHudTimeColor, true));
        var theme = enableTimeColor
            ? stageSnapshot.Theme
            : WorkTimeStageThemeProvider.GetTheme(WorkTimeStage.OffWork);

        var displayXp = gainedXp > 0 ? gainedXp : EstimateGestureXp(hudInfo.Action);
        var hudFunEnabled = _settingsService.Get(SettingKeys.WorkBearHudFunEnabled, true);
        var funText = showFun && hudFunEnabled
            ? GetFunText(hudInfo.Action, stageSnapshot.Stage, dashboard.IsFishing, now)
            : string.Empty;
        var mergedActionCount = GetMergedActionCount(hudInfo.Action, now);
        if (showFun && enableTimeColor && !string.IsNullOrWhiteSpace(funText))
        {
            funText = $"{funText} · {theme.ShortStatusText}";
        }

        return new WorkstationHudSnapshot(
            funText,
            displayXp > 0 ? $"经验 +{displayXp}" : string.Empty,
            showLevel ? level.LevelText : string.Empty,
            showLevel ? level.XpText : string.Empty,
            showLevel ? level.ProgressPercent : 0d,
            showStatusText ? GetWorkSummaryText(dashboard, hudInfo, mergedActionCount) : string.Empty,
            showStatusText ? $"手势 {dashboard.GestureCount} · 复制 {dashboard.CopyCount} · 粘贴 {dashboard.PasteCount} · 少点 {dashboard.EstimatedSavedClicks} 次" : string.Empty,
            showStatusText ? theme.ShortStatusText : string.Empty,
            showLevel,
            stageSnapshot.Stage,
            theme.Key,
            theme.StartColor,
            theme.EndColor,
            theme.AccentColor,
            theme.FriendlyColorName);
    }

    private async Task<(WorkstationDashboardSnapshot Dashboard, WorkerLevelSnapshot Level)> GetCachedSnapshotsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (TryGetCachedSnapshots(now, out var cachedDashboard, out var cachedLevel))
        {
            return (cachedDashboard, cachedLevel);
        }

        await _snapshotCacheLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedSnapshots(now, out cachedDashboard, out cachedLevel))
            {
                return (cachedDashboard, cachedLevel);
            }

            var dashboard = await _dashboardService.GetSnapshotAsync(now, cancellationToken);
            var level = await _workerLevelService.GetSnapshotAsync(cancellationToken);

            _cachedDashboard = dashboard;
            _cachedLevel = level;
            _snapshotCacheTimestamp = now;

            return (dashboard, level);
        }
        finally
        {
            _snapshotCacheLock.Release();
        }
    }

    private bool TryGetCachedSnapshots(
        DateTimeOffset now,
        out WorkstationDashboardSnapshot dashboard,
        out WorkerLevelSnapshot level)
    {
        if (_snapshotCacheTimestamp is { } cachedAt &&
            _cachedDashboard is { } cachedDashboard &&
            _cachedLevel is { } cachedLevel &&
            (now - cachedAt).Duration() < SnapshotCacheDuration)
        {
            dashboard = cachedDashboard;
            level = cachedLevel;
            return true;
        }

        dashboard = null!;
        level = null!;
        return false;
    }

    private static int EstimateGestureXp(BuiltInGestureAction action)
    {
        return action == BuiltInGestureAction.None
            ? 0
            : WorkerLevelService.GetActionXp(action) + 2;
    }

    private int GetMergedActionCount(BuiltInGestureAction action, DateTimeOffset now)
    {
        if (action is BuiltInGestureAction.None)
        {
            return 1;
        }

        lock (_mergeLock)
        {
            if (action == _lastMergedAction && now - _lastMergedAt <= ActionMergeWindow)
            {
                _mergedActionCount++;
            }
            else
            {
                _lastMergedAction = action;
                _mergedActionCount = 1;
            }

            _lastMergedAt = now;
            return _mergedActionCount;
        }
    }

    private static string GetWorkSummaryText(WorkstationDashboardSnapshot dashboard, GestureHudInfo hudInfo, int mergedActionCount)
    {
        var actionPrefix = hudInfo.Action switch
        {
            BuiltInGestureAction.Copy => mergedActionCount > 1 ? $"复制 +{mergedActionCount} · 少点 +{mergedActionCount}" : "复制成功 · 少点 +1",
            BuiltInGestureAction.Paste => mergedActionCount > 1 ? $"粘贴 +{mergedActionCount} · 今日已赚 {FormatMoney(dashboard.TodayEarned)}" : $"粘贴成功 · 今日已赚 {FormatMoney(dashboard.TodayEarned)}",
            BuiltInGestureAction.OpenClipboardOverlay => $"剪贴板打开 · 今日 {dashboard.OpenClipboardCount} 次",
            _ when dashboard.SprintActive => "手势完成 · 已进入下班冲刺",
            _ => $"今日 {FormatMoney(dashboard.TodayEarned)} · 下班 {FormatDuration(dashboard.TimeUntilOffWork)} · 发薪 {FormatPayday(dashboard.DaysUntilPayday)}"
        };

        return actionPrefix.Length <= 36
            ? actionPrefix
            : actionPrefix[..36];
    }
    private string GetFunText(
        BuiltInGestureAction action,
        WorkTimeStage stage,
        bool isFishing,
        DateTimeOffset now)
    {
        var style = WorkBearTextProvider.ParseStyle(
            _settingsService.Get(SettingKeys.WorkBearTextStyle, "打工人模式"));
        var stageLine = WorkBearTextProvider.HudFunLine(stage, isFishing, style, now);

        var actionLine = action switch
        {
            BuiltInGestureAction.Copy => "复制成功",
            BuiltInGestureAction.Paste or BuiltInGestureAction.SmartPaste => "粘贴成功",
            BuiltInGestureAction.PasteAndEnter => "粘贴并回车",
            BuiltInGestureAction.OpenClipboardOverlay => "剪贴板已开",
            BuiltInGestureAction.OpenQuickActionCenter => "快捷动作",
            BuiltInGestureAction.Enter => "回车确认",
            BuiltInGestureAction.Escape => "及时撤退",
            BuiltInGestureAction.None => "未绑定动作",
            _ when action.ToString().StartsWith("Assistant", StringComparison.Ordinal) => "文本处理完成",
            _ => "手势生效"
        };

        return $"{actionLine} · {stageLine}";
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


