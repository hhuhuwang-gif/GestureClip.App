using GestureClip.Core.Workstation;

namespace GestureClip.Features.Workstation;

public static class WorkTimeStageThemeProvider
{
    public static WorkTimeHudTheme GetTheme(WorkTimeStage stage) => stage switch
    {
        WorkTimeStage.EarlyWork => new("GreenGradient", "#063B32", "#0F766E", "#A7F3D0", "开工状态", "绿色"),
        WorkTimeStage.MidWork => new("BlueGradient", "#111827", "#1D4ED8", "#BFDBFE", "稳定输出", "蓝色"),
        WorkTimeStage.LateWork => new("RedGradient", "#431407", "#B91C1C", "#FED7AA", "注意休息", "红色"),
        WorkTimeStage.Overtime => new("DeepRedGradient", "#1F0505", "#7F1D1D", "#FECACA", "加班中", "深红"),
        WorkTimeStage.LunchBreak => new("YellowGradient", "#422006", "#854D0E", "#FEF08A", "午休中", "黄色"),
        WorkTimeStage.RestDay => new("NeutralGradient", "#111827", "#334155", "#E2E8F0", "休息日", "灰白"),
        WorkTimeStage.BeforeWork => new("NeutralGradient", "#111827", "#334155", "#E2E8F0", "未上班", "灰白"),
        _ => new("NeutralGradient", "#111827", "#334155", "#E2E8F0", "已下班", "灰白")
    };
}
