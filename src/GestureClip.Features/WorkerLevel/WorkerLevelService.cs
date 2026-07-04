using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Core.WorkerLevel;

namespace GestureClip.Features.WorkerLevel;

public sealed class WorkerLevelService : IWorkerLevelService
{
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

    public WorkerLevelService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task<WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var totalXp = Math.Max(0, _settingsService.Get(SettingKeys.WorkerLevelTotalXp, 0));
        var actionCount = Math.Max(0, _settingsService.Get(SettingKeys.WorkerLevelTotalActionCount, 0));
        var storedLevel = Math.Clamp(_settingsService.Get(SettingKeys.WorkerLevelCurrentLevel, 1), 1, 10);
        var computedLevel = GetLevelForXp(totalXp).Level;
        var level = storedLevel > computedLevel ? computedLevel : storedLevel;
        if (level != computedLevel)
        {
            level = computedLevel;
        }

        var lastLevelUpText = _settingsService.Get(SettingKeys.WorkerLevelLastLevelUpAt, string.Empty);
        var lastLevelUpAt = DateTimeOffset.TryParse(lastLevelUpText, out var parsed) ? parsed : (DateTimeOffset?)null;
        return Task.FromResult(CreateSnapshot(totalXp, actionCount, level, leveledUp: false, previousLevel: level, lastLevelUpAt));
    }

    public async Task<WorkerLevelSnapshot> RecordActionAsync(
        BuiltInGestureAction action,
        bool isGestureSuccess,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var oldXp = Math.Max(0, _settingsService.Get(SettingKeys.WorkerLevelTotalXp, 0));
            var oldLevel = GetLevelForXp(oldXp).Level;
            var oldCount = Math.Max(0, _settingsService.Get(SettingKeys.WorkerLevelTotalActionCount, 0));
            var gained = GetActionXp(action) + (isGestureSuccess ? 2 : 0);
            var newXp = Math.Max(0, oldXp + gained);
            var newLevel = GetLevelForXp(newXp).Level;
            var newCount = oldCount + 1;
            var leveledUp = newLevel > oldLevel;
            var lastLevelUpAt = leveledUp ? now : TryReadLastLevelUpAt();

            await _settingsService.SetAsync(SettingKeys.WorkerLevelTotalXp, newXp, cancellationToken);
            await _settingsService.SetAsync(SettingKeys.WorkerLevelCurrentLevel, newLevel, cancellationToken);
            await _settingsService.SetAsync(SettingKeys.WorkerLevelTotalActionCount, newCount, cancellationToken);
            if (leveledUp)
            {
                await _settingsService.SetAsync(SettingKeys.WorkerLevelLastLevelUpAt, now.ToString("O"), cancellationToken);
            }

            return CreateSnapshot(newXp, newCount, newLevel, leveledUp, leveledUp ? oldLevel : newLevel, lastLevelUpAt);
        }
        finally
        {
            _sync.Release();
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
}
