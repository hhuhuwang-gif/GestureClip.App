using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using GestureClip.Features.Workstation;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class OverworkReminderServiceTests
{
    [Fact]
    public async Task CheckNowAsync_does_not_show_when_disabled()
    {
        var settings = CreateSettings();
        settings.Values[SettingKeys.WorkstationEnableOverworkReminder] = false;
        var toast = new FakeToast();
        var service = new OverworkReminderService(settings, new WorkTimeStageService(settings), toast);

        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T10:30:00+08:00"), CancellationToken.None);

        Assert.Equal(0, toast.ShowCount);
    }

    [Fact]
    public async Task CheckNowAsync_shows_continuous_work_reminder_with_cooldown()
    {
        var settings = CreateSettings();
        settings.Values[SettingKeys.WorkstationOverworkReminderIntervalMinutes] = 60;
        var toast = new FakeToast();
        var service = new OverworkReminderService(settings, new WorkTimeStageService(settings), toast);

        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T10:05:00+08:00"), CancellationToken.None);
        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T10:10:00+08:00"), CancellationToken.None);

        Assert.Equal(1, toast.ShowCount);
        Assert.Contains("连续工作", toast.LastMessage);
        Assert.DoesNotContain("猝死", toast.LastMessage);
    }

    [Fact]
    public async Task Snooze_delays_next_reminder()
    {
        var settings = CreateSettings();
        settings.Values[SettingKeys.WorkstationOverworkSnoozeMinutes] = 15;
        var toast = new FakeToast { Result = OverworkReminderToastResult.Snooze };
        var service = new OverworkReminderService(settings, new WorkTimeStageService(settings), toast);

        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T10:05:00+08:00"), CancellationToken.None);
        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T10:10:00+08:00"), CancellationToken.None);
        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T10:21:00+08:00"), CancellationToken.None);

        Assert.Equal(2, toast.ShowCount);
    }

    [Fact]
    public async Task Strong_warning_uses_stronger_but_non_medical_text_for_high_risk()
    {
        var settings = CreateSettings();
        settings.Values[SettingKeys.WorkstationEnableStrongOverworkWarning] = true;
        settings.Values[SettingKeys.WorkstationOverworkHighRiskAfterHours] = 8d;
        var toast = new FakeToast();
        var service = new OverworkReminderService(settings, new WorkTimeStageService(settings), toast);

        await service.CheckNowAsync(DateTimeOffset.Parse("2026-07-06T18:05:00+08:00"), CancellationToken.None);

        Assert.Equal(1, toast.ShowCount);
        Assert.Contains("建议现在就停一下", toast.LastMessage);
        Assert.DoesNotContain("猝死", toast.LastMessage);
        Assert.DoesNotContain("医学", toast.LastMessage);
    }

    private static FakeSettingsService CreateSettings()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationEnableOverworkReminder] = true;
        settings.Values[SettingKeys.WorkstationOverworkReminderIntervalMinutes] = 60;
        settings.Values[SettingKeys.WorkstationOverworkHighRiskAfterHours] = 8d;
        settings.Values[SettingKeys.WorkstationEnableStrongOverworkWarning] = false;
        settings.Values[SettingKeys.WorkstationOverworkReminderCanSnooze] = true;
        settings.Values[SettingKeys.WorkstationOverworkSnoozeMinutes] = 15;
        settings.Values[SettingKeys.WorkstationWorkStartTime] = "09:00";
        settings.Values[SettingKeys.WorkstationWorkEndTime] = "18:00";
        settings.Values[SettingKeys.WorkstationLunchStartTime] = "12:00";
        settings.Values[SettingKeys.WorkstationLunchEndTime] = "13:00";
        settings.Values[SettingKeys.WorkstationWorkdays] = "1,2,3,4,5";
        return settings;
    }

    private sealed class FakeToast : IOverworkReminderToastService
    {
        public int ShowCount { get; private set; }
        public string LastMessage { get; private set; } = string.Empty;
        public OverworkReminderToastResult Result { get; set; } = OverworkReminderToastResult.Dismiss;

        public Task<OverworkReminderToastResult> ShowAsync(OverworkReminderNotification notification, CancellationToken cancellationToken)
        {
            ShowCount++;
            LastMessage = notification.Message;
            return Task.FromResult(Result);
        }
    }
}

