using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using GestureClip.Features.Workstation;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class WellnessReminderServiceTests
{
    // 10:30 on a Monday with default 09:00-18:00 schedule → EarlyWork/MidWork stage.
    private static readonly DateTimeOffset WorkTime = DateTimeOffset.Parse("2026-07-06T10:30:00+08:00");
    private static readonly DateTimeOffset BeforeWorkTime = DateTimeOffset.Parse("2026-07-06T05:00:00+08:00");

    [Fact]
    public async Task Disabled_by_default_shows_nothing()
    {
        var (service, toast) = CreateService(new FakeSettingsService());

        await service.CheckNowAsync(WorkTime, CancellationToken.None);

        Assert.Equal(0, toast.ShowCount);
    }

    [Fact]
    public async Task Water_reminder_fires_then_respects_interval()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WellnessWaterReminderEnabled] = true;
        settings.Values[SettingKeys.WellnessWaterIntervalMinutes] = 60;
        var (service, toast) = CreateService(settings);

        await service.CheckNowAsync(WorkTime, CancellationToken.None);
        Assert.Equal(1, toast.ShowCount);
        Assert.Contains("喝水", toast.LastTitle);

        await service.CheckNowAsync(WorkTime.AddMinutes(30), CancellationToken.None);
        Assert.Equal(1, toast.ShowCount);

        await service.CheckNowAsync(WorkTime.AddMinutes(61), CancellationToken.None);
        Assert.Equal(2, toast.ShowCount);
    }

    [Fact]
    public async Task Stretch_reminder_does_not_stack_with_water_on_same_tick()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WellnessWaterReminderEnabled] = true;
        settings.Values[SettingKeys.WellnessStretchReminderEnabled] = true;
        settings.Values[SettingKeys.WellnessWaterIntervalMinutes] = 60;
        settings.Values[SettingKeys.WellnessStretchIntervalMinutes] = 60;
        var (service, toast) = CreateService(settings);

        await service.CheckNowAsync(WorkTime, CancellationToken.None);
        Assert.Equal(1, toast.ShowCount);
        Assert.Contains("喝水", toast.LastTitle);

        await service.CheckNowAsync(WorkTime.AddMinutes(1), CancellationToken.None);
        Assert.Equal(2, toast.ShowCount);
        Assert.Contains("拉伸", toast.LastTitle);
    }

    [Fact]
    public async Task Outside_work_stage_shows_nothing()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WellnessWaterReminderEnabled] = true;
        var (service, toast) = CreateService(settings);

        await service.CheckNowAsync(BeforeWorkTime, CancellationToken.None);

        Assert.Equal(0, toast.ShowCount);
    }

    private static (WellnessReminderService Service, FakeToast Toast) CreateService(FakeSettingsService settings)
    {
        var toast = new FakeToast();
        return (new WellnessReminderService(settings, new WorkTimeStageService(settings), toast), toast);
    }

    private sealed class FakeToast : IOverworkReminderToastService
    {
        public int ShowCount { get; private set; }
        public string LastTitle { get; private set; } = string.Empty;

        public Task<OverworkReminderToastResult> ShowAsync(OverworkReminderNotification notification, CancellationToken cancellationToken)
        {
            ShowCount++;
            LastTitle = notification.Title;
            return Task.FromResult(OverworkReminderToastResult.Dismiss);
        }
    }
}
