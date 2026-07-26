namespace GestureClip.Core.Abstractions;

public interface IAppLifecycleService
{
    bool IsExplicitExit { get; }

    void ShowSettingsWindow(string? page = null);

    /// <summary>Open settings on action-binding page and focus a gesture pattern (e.g. U, D, UL).</summary>
    void ShowGestureBindingEditor(string? pattern = null);

    void ToggleSettingsWindow();

    void ShowWorkstationDashboardWindow();

    /// <summary>Toggle the always-on-top WorkBear mini widget (earnings + countdown pill).</summary>
    void ToggleWorkBearWidget()
    {
    }

    /// <summary>Show the mini widget on startup when the user previously enabled it.</summary>
    void ShowWorkBearWidgetIfEnabled()
    {
    }

    void OpenLatestReleasePage();

    Task CheckForUpdatesAsync();

    Task StartCoverUpdateAsync();

    void ExitApplication();
}
