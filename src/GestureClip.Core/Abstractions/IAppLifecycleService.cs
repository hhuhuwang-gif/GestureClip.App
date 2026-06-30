namespace GestureClip.Core.Abstractions;

public interface IAppLifecycleService
{
    bool IsExplicitExit { get; }

    void ShowSettingsWindow();

    void ExitApplication();
}
