namespace GestureClip.Core.Abstractions;

public interface IAppLifecycleService
{
    bool IsExplicitExit { get; }

    void ShowSettingsWindow();

    void ShowWorkstationDashboardWindow();

    void OpenLatestReleasePage();

    void ExitApplication();
}
