using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;

namespace GestureClip.Features.Onboarding;

public sealed class FirstRunOnboardingService : IFirstRunOnboardingService
{
    private readonly ISettingsService _settingsService;

    public FirstRunOnboardingService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool ShouldShowOnboarding()
    {
        return !_settingsService.Get(SettingKeys.AppOnboardingCompleted, false);
    }

    public Task CompleteAsync(CancellationToken cancellationToken)
    {
        return _settingsService.SetAsync(SettingKeys.AppOnboardingCompleted, true, cancellationToken);
    }
}
