namespace GestureClip.Core.WorkerLevel;

public sealed record WorkerLevelSnapshot(
    int TotalXp,
    int TotalActionCount,
    WorkerLevelDefinition CurrentLevel,
    WorkerLevelDefinition NextLevel,
    int XpIntoCurrentLevel,
    int XpNeededForNextLevel,
    double ProgressPercent,
    bool LeveledUp,
    int PreviousLevel,
    DateTimeOffset? LastLevelUpAt)
{
    public string LevelText => $"Lv.{CurrentLevel.Level} {CurrentLevel.Title}";

    public string XpText => CurrentLevel.Level >= NextLevel.Level
        ? $"XP {TotalXp} / MAX"
        : $"XP {TotalXp} / {NextLevel.RequiredXp}";
}
