using GestureClip.Core.Settings;
using GestureClip.Features.Onboarding;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Onboarding;

public sealed class FirstRunOnboardingServiceTests
{
    [Fact]
    public void ShouldShowOnboarding_returns_true_until_onboarding_is_completed()
    {
        var settings = new FakeSettingsService();
        var service = new FirstRunOnboardingService(settings);

        Assert.True(service.ShouldShowOnboarding());
    }

    [Fact]
    public void ShouldShowOnboarding_returns_false_after_completion_setting_is_saved()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.AppOnboardingCompleted] = true;
        var service = new FirstRunOnboardingService(settings);

        Assert.False(service.ShouldShowOnboarding());
    }

    [Fact]
    public async Task CompleteAsync_marks_onboarding_completed_once()
    {
        var settings = new FakeSettingsService();
        var service = new FirstRunOnboardingService(settings);

        await service.CompleteAsync(CancellationToken.None);

        Assert.Equal(true, settings.Values[SettingKeys.AppOnboardingCompleted]);
        Assert.Equal(1, settings.SetCountsByKey[SettingKeys.AppOnboardingCompleted]);
    }
}
