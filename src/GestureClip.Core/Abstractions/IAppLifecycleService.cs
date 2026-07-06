namespace GestureClip.Core.Abstractions;

public interface IAppLifecycleService
{
    bool IsExplicitExit { get; }

    void ShowSettingsWindow();

    void ToggleSettingsWindow();

    void ShowWorkstationDashboardWindow();

    void OpenLatestReleasePage();

    Task CheckForUpdatesAsync();

    Task StartCoverUpdateAsync();

    void ExitApplication();
}
