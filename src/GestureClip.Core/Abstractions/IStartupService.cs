namespace GestureClip.Core.Abstractions;

public interface IStartupService
{
    bool IsEnabled();

    void Enable();

    void Disable();

    string GetStartupCommand();

    bool IsDevelopmentRunMode();
}
