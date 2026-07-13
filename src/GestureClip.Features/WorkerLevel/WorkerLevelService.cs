using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Core.WorkerLevel;

namespace GestureClip.Features.WorkerLevel;

public sealed class WorkerLevelService : IWorkerLevelService
{
    private const int PersistEveryActionCount = 10;

    public static readonly IReadOnlyList<WorkerLevelDefinition> LevelDefinitions =
    [
        new(1, 0, "初入工位"),
        new(2, 50, "复制学徒"),
        new(3, 120, "粘贴熟练工"),
        new(4, 250, "摸鱼见习生"),
        new(5, 500, "右键小法师"),
        new(6, 900, "工位老油条"),
        new(7, 1400, "快捷键修行者"),
        new(8, 2200, "牛马效率王"),
        new(9, 3200, "下班守望者"),
        new(10, 5000, "终极打工战神")
    ];

    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private WorkerLevelState? _cachedState;
    private int _actionsSincePersist;

    public WorkerLevelService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var state = GetState();
            return CreateSnapshot(
                state.TotalXp,
                state.ActionCount,
                GetLevelForXp(state.TotalXp).Level,
                leveledUp: false,
                previousLevel: GetLevelForXp(state.TotalXp).Level,
                state.LastLevelUpAt);
        }
        finally
        {
            _sync.Release();
        }
    }

    public Task<WorkerLevelSnapshot> RecordActionAsync(
        BuiltInGestureAction action,
        bool isGestureSuccess,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var gained = GetActionXp(action) + (isGestureSuccess ? 2 : 0);
        return ApplyXpAsync(gained, countAsAction: true, now, cancellationToken);
    }

    public Task<WorkerLevelSnapshot> RecordBonusXpAsync(
        int xp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return ApplyXpAsync(Math.Max(0, xp), countAsAction: true, now, cancellationToken);
    }

    private async Task<WorkerLevelSnapshot> ApplyXpAsync(
        int gained,
        bool countAsAction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var state = GetState();
            var oldXp = state.TotalXp;
            var oldLevel = GetLevelForXp(oldXp).Level;
            var newXp = Math.Max(0, oldXp + gained);
            var newLevel = GetLevelForXp(newXp).Level;
            var newCount = countAsAction ? state.ActionCount + 1 : state.ActionCount;
            var leveledUp = newLevel > oldLevel;
            var lastLevelUpAt = leveledUp ? now : state.LastLevelUpAt;
            _cachedState = new WorkerLevelState(newXp, newCount, lastLevelUpAt);
            if (countAsAction)
            {
                _actionsSincePersist++;
            }

            if (leveledUp || _actionsSincePersist >= PersistEveryActionCount)
            {
                await PersistStateAsync(_cachedState, newLevel, leveledUp, now, cancellationToken);
                _actionsSincePersist = 0;
            }

            return CreateSnapshot(newXp, newCount, newLevel, leveledUp, leveledUp ? oldLevel : newLevel, lastLevelUpAt);
        }
        finally
        {
            _sync.Release();
        }
    }

    private WorkerLevelState GetState()
    {
        if (_cachedState is { } cached)
        {
            return cached;
        }

        var totalXp = Math.Max(0, _settingsService.Get(SettingKeys.WorkerLevelTotalXp, 0));
        var actionCount = Math.Max(0, _settingsService.Get(SettingKeys.WorkerLevelTotalActionCount, 0));
        var lastLevelUpAt = TryReadLastLevelUpAt();
        _cachedState = new WorkerLevelState(totalXp, actionCount, lastLevelUpAt);
        return _cachedState;
    }

    private async Task PersistStateAsync(
        WorkerLevelState state,
        int currentLevel,
        bool leveledUp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await _settingsService.SetAsync(SettingKeys.WorkerLevelTotalXp, state.TotalXp, cancellationToken);
        await _settingsService.SetAsync(SettingKeys.WorkerLevelCurrentLevel, currentLevel, cancellationToken);
        await _settingsService.SetAsync(SettingKeys.WorkerLevelTotalActionCount, state.ActionCount, cancellationToken);
        if (leveledUp)
        {
            await _settingsService.SetAsync(SettingKeys.WorkerLevelLastLevelUpAt, now.ToString("O"), cancellationToken);
        }
    }

    public static int GetActionXp(BuiltInGestureAction action)
    {
        return action switch
        {
            BuiltInGestureAction.Copy or
            BuiltInGestureAction.Paste or
            BuiltInGestureAction.Cut or
            BuiltInGestureAction.SelectAll or
            BuiltInGestureAction.Undo or
            BuiltInGestureAction.Redo or
            BuiltInGestureAction.Enter or
            BuiltInGestureAction.Escape or
            BuiltInGestureAction.SendAltLeft or
            BuiltInGestureAction.SendAltRight => 1,
            BuiltInGestureAction.OpenClipboardOverlay => 2,
            BuiltInGestureAction.PasteLatestClipboardItem => 3,
            _ => 1
        };
    }

    private DateTimeOffset? TryReadLastLevelUpAt()
    {
        var text = _settingsService.Get(SettingKeys.WorkerLevelLastLevelUpAt, string.Empty);
        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static WorkerLevelSnapshot CreateSnapshot(
        int totalXp,
        int actionCount,
        int levelNumber,
        bool leveledUp,
        int previousLevel,
        DateTimeOffset? lastLevelUpAt)
    {
        var current = LevelDefinitions.Single(level => level.Level == levelNumber);
        var next = LevelDefinitions.FirstOrDefault(level => level.Level > current.Level) ?? current;
        var previousThreshold = current.RequiredXp;
        var nextThreshold = next.RequiredXp;
        var needed = current.Level >= next.Level ? 0 : Math.Max(1, nextThreshold - previousThreshold);
        var intoLevel = current.Level >= next.Level ? 0 : Math.Clamp(totalXp - previousThreshold, 0, needed);
        var percent = current.Level >= next.Level ? 1d : Math.Clamp((double)intoLevel / needed, 0d, 1d);

        return new WorkerLevelSnapshot(
            totalXp,
            actionCount,
            current,
            next,
            intoLevel,
            needed,
            percent,
            leveledUp,
            previousLevel,
            lastLevelUpAt);
    }

    private static WorkerLevelDefinition GetLevelForXp(int totalXp)
    {
        return LevelDefinitions.Last(level => totalXp >= level.RequiredXp);
    }

    private sealed record WorkerLevelState(int TotalXp, int ActionCount, DateTimeOffset? LastLevelUpAt);
}
