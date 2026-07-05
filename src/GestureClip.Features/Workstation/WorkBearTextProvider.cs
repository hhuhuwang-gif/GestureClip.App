using GestureClip.Core.Workstation;

namespace GestureClip.Features.Workstation;

public static class WorkBearTextProvider
{
    public static WorkBearTextStyle ParseStyle(string? value)
    {
        return value switch
        {
            "正常模式" or "Normal" => WorkBearTextStyle.Normal,
            "抽象模式" or "Abstract" => WorkBearTextStyle.Abstract,
            _ => WorkBearTextStyle.Worker
        };
    }

    public static string StageText(WorkTimeStage stage) => stage switch
    {
        WorkTimeStage.BeforeWork => "未上班",
        WorkTimeStage.EarlyWork or WorkTimeStage.MidWork => "上班中",
        WorkTimeStage.LateWork => "即将下班",
        WorkTimeStage.LunchBreak => "午休中",
        WorkTimeStage.Overtime => "加班中",
        WorkTimeStage.RestDay => "休息日",
        _ => "已下班"
    };

    public static string BearStatus(WorkTimeStage stage, bool isFishing) => isFishing
        ? "摸鱼小熊"
        : stage switch
        {
            WorkTimeStage.BeforeWork => "活力小熊",
            WorkTimeStage.EarlyWork or WorkTimeStage.MidWork => "专注小熊",
            WorkTimeStage.LateWork => "收尾小熊",
            WorkTimeStage.LunchBreak => "午休小熊",
            WorkTimeStage.Overtime => "加班小熊",
            WorkTimeStage.RestDay => "休息日小熊",
            _ => "低功耗小熊"
        };

    public static string BearLine(WorkTimeStage stage, WorkBearTextStyle style, bool isFishing, TimeSpan untilOffWork)
    {
        if (isFishing)
        {
            return style == WorkBearTextStyle.Normal ? "摸鱼计时中，结束后会计入今日报告。" : "小熊已进入静默观察模式。";
        }

        return (style, stage) switch
        {
            (WorkBearTextStyle.Normal, WorkTimeStage.BeforeWork) => "还没到上班时间，可以先准备今天的工作。",
            (WorkBearTextStyle.Normal, WorkTimeStage.LateWork) => "快下班了，建议开始收尾。",
            (WorkBearTextStyle.Normal, WorkTimeStage.Overtime) => "已进入加班时间，建议保存文件并休息。",
            (WorkBearTextStyle.Normal, WorkTimeStage.RestDay) => "今天是休息日，注意安排时间。",
            (WorkBearTextStyle.Abstract, WorkTimeStage.LateWork) => "牛马电量偏低，建议进入体面撤退流程。",
            (WorkBearTextStyle.Abstract, WorkTimeStage.Overtime) => "工位引力增强，小熊建议保存文件后脱离轨道。",
            (WorkBearTextStyle.Abstract, WorkTimeStage.LunchBreak) => "灵魂离线窗口开启，午休属于系统维护。",
            (_, WorkTimeStage.BeforeWork) => "刚开工，小熊还活着。",
            (_, WorkTimeStage.EarlyWork or WorkTimeStage.MidWork) => "今日输出稳定，继续苟住。",
            (_, WorkTimeStage.LateWork) => untilOffWork <= TimeSpan.FromMinutes(30) ? "快下班了，建议开始收尾。" : "后半程已到，稳住节奏。",
            (_, WorkTimeStage.Overtime) => "已进入加班区，建议保存文件并撤退。",
            (_, WorkTimeStage.RestDay) => "休息日还在用？你是真的拼。",
            (_, WorkTimeStage.LunchBreak) => "午休中，小熊建议补充能量。",
            _ => "今日目标：活着下班。"
        };
    }

    public static string SprintSuggestion(WorkTimeStage stage, TimeSpan untilOffWork, WorkBearTextStyle style)
    {
        if (stage == WorkTimeStage.Overtime)
        {
            return style == WorkBearTextStyle.Normal ? "保存文件，整理明天待办，然后结束工作。" : "已进入加班区，小熊建议开始收尾。";
        }

        if (untilOffWork <= TimeSpan.FromMinutes(30) && untilOffWork > TimeSpan.Zero)
        {
            return style == WorkBearTextStyle.Abstract ? "开始体面撤退，不要最后一分钟崩盘。" : "保存文件 / 整理明天待办 / 准备撤退";
        }

        return "保持节奏，稳住输出。";
    }

    public static string RestRisk(TimeSpan continuousWork, WorkTimeStage stage)
    {
        if (stage == WorkTimeStage.Overtime || continuousWork.TotalHours >= 8)
        {
            return "已超时工作";
        }

        if (continuousWork.TotalMinutes >= 120)
        {
            return "建议活动";
        }

        if (continuousWork.TotalMinutes >= 60)
        {
            return "注意休息";
        }

        return "正常";
    }

    public static string Rating(TimeSpan workDuration, TimeSpan fishingDuration, TimeSpan overtime, int savedClicks)
    {
        if (fishingDuration.TotalMinutes >= 60)
        {
            return "摸鱼艺术家";
        }

        if (overtime.TotalMinutes >= 90)
        {
            return "加班战神";
        }

        if (workDuration.TotalHours >= 9.5)
        {
            return "高强度燃烧型";
        }

        if (savedClicks >= 100)
        {
            return "稳定输出型";
        }

        return overtime <= TimeSpan.Zero ? "准时撤退型" : "工位濒危物种";
    }
}
