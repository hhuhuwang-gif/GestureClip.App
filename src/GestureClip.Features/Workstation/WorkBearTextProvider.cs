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
        WorkTimeStage.BeforeWork => "☕ 未上班",
        WorkTimeStage.EarlyWork => "🌅 上午开工",
        WorkTimeStage.MidWork => "🛠 午后稳住",
        WorkTimeStage.LateWork => "🏔 即将下班",
        WorkTimeStage.LunchBreak => "🍱 午休中",
        WorkTimeStage.Overtime => "🌙 加班中",
        WorkTimeStage.RestDay => "🌿 休息日",
        _ => "🏠 已下班"
    };

    public static string BearStatus(WorkTimeStage stage, bool isFishing) => isFishing
        ? "🐟 摸鱼小熊"
        : stage switch
        {
            WorkTimeStage.BeforeWork => "⚡ 活力小熊",
            WorkTimeStage.EarlyWork => "🌻 晨间小熊",
            WorkTimeStage.MidWork => "🎯 专注小熊",
            WorkTimeStage.LateWork => "🏁 收尾小熊",
            WorkTimeStage.LunchBreak => "😴 午休小熊",
            WorkTimeStage.Overtime => "🔥 加班小熊",
            WorkTimeStage.RestDay => "🌴 休息日小熊",
            _ => "🔋 低功耗小熊"
        };

    /// <summary>Compact emoji used by HUD / cards as WorkBear IP mark.</summary>
    public static string BearEmoji(WorkTimeStage stage, bool isFishing) => isFishing
        ? "🐟"
        : stage switch
        {
            WorkTimeStage.BeforeWork => "⚡",
            WorkTimeStage.EarlyWork => "🌻",
            WorkTimeStage.MidWork => "🎯",
            WorkTimeStage.LateWork => "🏁",
            WorkTimeStage.LunchBreak => "😴",
            WorkTimeStage.Overtime => "🔥",
            WorkTimeStage.RestDay => "🌴",
            _ => "🐻"
        };

    public static string BearLine(
        WorkTimeStage stage,
        WorkBearTextStyle style,
        bool isFishing,
        TimeSpan untilOffWork,
        DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.Now;
        if (isFishing)
        {
            return Pick(style, clock, 0,
                "摸鱼计时中，结束后会计入今日报告。",
                "小熊已进入静默观察模式。",
                "计时进行中，记得回来点结束。");
        }

        return stage switch
        {
            WorkTimeStage.BeforeWork => PickStyle(style, clock, 1,
                normal: ["还没到上班时间，可以先整理今天的待办。", "开工前先把水和待办准备好。", "缓冲期：别急着把自己点着。"],
                worker: ["刚开机，小熊还活着。", "开工倒计时，先把工位点亮。", "今日目标：体面上班，准时下班。"],
                abstractLines: ["工位引擎预热中。", "系统尚未进入生产模式。", "牛马协议加载中……"]),

            WorkTimeStage.EarlyWork => PickStyle(style, clock, 2,
                normal: ["上午适合推进重要事项，先拿下最难的一件。", "晨间输出期，优先处理高优先级工作。", "开局稳住节奏，别把简单事变复杂。"],
                worker: ["上午还撑得住，先把硬骨头啃了。", "今日输出刚启动，继续苟住。", "早场能量还在，别浪费在群聊上。"],
                abstractLines: ["晨间算力在线。", "高优先级任务窗口开启。", "牛马早班班次正常运行。"]),

            WorkTimeStage.MidWork => PickStyle(style, clock, 3,
                normal: ["午后容易走神，建议切到专注时段。", "下午适合处理沟通和协作类事务。", "后半场开始了，保持稳定输出。"],
                worker: ["午后犯困是常态，起来喝口水再战。", "今日输出稳定，继续苟住。", "别在下午三点开又臭又长的会。"],
                abstractLines: ["午后算力波动，建议重启专注。", "中段推进协议生效。", "工位重力略增，稳住航线。"]),

            WorkTimeStage.LateWork => untilOffWork <= TimeSpan.FromMinutes(30) && untilOffWork > TimeSpan.Zero
                ? PickStyle(style, clock, 4,
                    normal: ["快下班了，建议开始收尾和保存文件。", "临下班半小时，优先处理可关闭事项。", "开始整理明天待办，别留尾巴。"],
                    worker: ["快下班了，建议开始收尾。", "后半程收尾，别接新需求。", "距离自由还有一点点，稳住。"],
                    abstractLines: ["牛马电量偏低，建议进入体面撤退流程。", "撤退窗口开启，请勿新增任务。", "下班协议准备执行。"])
                : PickStyle(style, clock, 5,
                    normal: ["后半程已到，适合收口和确认进度。", "快到下班线，优先完成可交付项。", "进入收尾准备，避免临时加塞。"],
                    worker: ["后半程已到，稳住节奏。", "别在下班前接黑盒需求。", "收尾阶段，保存比完美更重要。"],
                    abstractLines: ["收尾阶段锁定。", "输出窗口即将关闭。", "工位引力增强，准备脱离。"]),

            WorkTimeStage.LunchBreak => PickStyle(style, clock, 6,
                normal: ["午休中，建议离开屏幕补充能量。", "短暂离线有助于下午效率。", "吃饭喝水，别边吃边回消息。"],
                worker: ["午休中，小熊建议补充能量。", "去吃点热乎的，别只喝咖啡。", "午休属于系统维护时间。"],
                abstractLines: ["灵魂离线窗口开启。", "午休属于系统维护。", "充电中，请勿打扰。"]),

            WorkTimeStage.Overtime => PickStyle(style, clock, 7,
                normal: ["已进入加班时间，建议保存文件并休息。", "加班中请优先保交付，不要无限拉长。", "建议设定一个离开时间并执行。"],
                worker: ["已进入加班区，建议保存文件并撤退。", "加班不是荣誉勋章，该撤撤。", "文件先存，身体也先保。"],
                abstractLines: ["工位引力增强，小熊建议保存文件后脱离轨道。", "加班模式已启用，注意安全退出。", "超额工时警告：建议发起体面撤退。"]),

            WorkTimeStage.RestDay => PickStyle(style, clock, 8,
                normal: ["今天是休息日，注意安排时间。", "休息日也在线？记得给自己留白。", "如果必须干活，先设好结束时间。"],
                worker: ["休息日还在用？你是真的拼。", "周末开机，建议降低期待。", "休息日目标：别把自己卷进工位。"],
                abstractLines: ["休息日协议冲突。", "非工作日仍检测到工位信号。", "建议切换到离线模式。"]),

            _ => PickStyle(style, clock, 9,
                normal: ["已下班，记得切换状态离开工位。", "工作时间结束，优先休息。", "今天也辛苦了，明天再战。"],
                worker: ["今日目标：活着下班。", "低功耗运行中，明天见。", "关机也能涨经验，先撤。"],
                abstractLines: ["工位进程已挂起。", "低功耗模式已启用。", "今日副本结算完成。"])
        };
    }

    // Backward-compatible overload used by older call sites/tests.
    public static string BearLine(WorkTimeStage stage, WorkBearTextStyle style, bool isFishing, TimeSpan untilOffWork) =>
        BearLine(stage, style, isFishing, untilOffWork, null);

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

        return style switch
        {
            WorkBearTextStyle.Normal => "保持节奏，稳住输出。",
            WorkBearTextStyle.Abstract => "输出协议正常，勿开启无限循环。",
            _ => "保持节奏，稳住输出，别接黑盒需求。"
        };
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

    public static string HudFunLine(WorkTimeStage stage, bool isFishing, WorkBearTextStyle style, DateTimeOffset now)
    {
        if (isFishing)
        {
            return style == WorkBearTextStyle.Abstract ? "静默观察 +1" : "摸鱼计时中";
        }

        return stage switch
        {
            WorkTimeStage.EarlyWork => Pick(style, now, 11, "上午输出 +1", "晨间启动", "早场稳住"),
            WorkTimeStage.MidWork => Pick(style, now, 12, "午后推进 +1", "专注中", "继续苟住"),
            WorkTimeStage.LateWork => Pick(style, now, 13, "收尾 +1", "准备撤退", "快下班了"),
            WorkTimeStage.Overtime => Pick(style, now, 14, "加班警告", "该撤了", "保存优先"),
            WorkTimeStage.LunchBreak => "午休充电",
            _ => Pick(style, now, 15, "效率 +1", "工位在线", "活着就好")
        };
    }

    private static string PickStyle(
        WorkBearTextStyle style,
        DateTimeOffset now,
        int salt,
        string[] normal,
        string[] worker,
        string[] abstractLines)
    {
        var pool = style switch
        {
            WorkBearTextStyle.Normal => normal,
            WorkBearTextStyle.Abstract => abstractLines,
            _ => worker
        };
        return PickFrom(pool, now, salt);
    }

    private static string Pick(WorkBearTextStyle style, DateTimeOffset now, int salt, params string[] lines) =>
        PickFrom(lines, now, salt + (int)style);

    private static string PickFrom(IReadOnlyList<string> lines, DateTimeOffset now, int salt)
    {
        if (lines.Count == 0)
        {
            return "今日目标：活着下班。";
        }

        // Rotate by day + hour so lines change through the day without feeling random spam.
        var index = Math.Abs((now.DayOfYear * 24 + now.Hour + salt) % lines.Count);
        return lines[index];
    }
}
