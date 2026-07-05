namespace GestureClip.Core.Workstation;

public sealed record WorkTimeStageSnapshot(
    WorkTimeStage Stage,
    double WorkProgress,
    TimeSpan EffectiveWorkedTime,
    WorkTimeHudTheme Theme)
{
    public string ShortStatusText => Theme.ShortStatusText;
}
