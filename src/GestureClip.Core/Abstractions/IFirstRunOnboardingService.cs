namespace GestureClip.Core.Abstractions;

public interface IFirstRunOnboardingService
{
    bool ShouldShowOnboarding();

    Task CompleteAsync(CancellationToken cancellationToken);
}
