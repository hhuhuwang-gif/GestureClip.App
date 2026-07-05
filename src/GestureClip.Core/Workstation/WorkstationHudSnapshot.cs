using GestureClip.Core.Gestures;

namespace GestureClip.Core.Workstation;

public sealed record WorkstationHudSnapshot(
    string FunText,
    string GainedXpText,
    string LevelText,
    string XpText,
    double XpProgressPercent,
    string WorkSummaryText,
    string StatsText,
    string WorkStatusText,
    bool ShowLevel,
    WorkTimeStage WorkTimeStage,
    string HudThemeKey,
    string HudStartColor,
    string HudEndColor,
    string HudAccentColor,
    string HudFriendlyColorName);
