namespace GestureClip.Core.Abstractions;

public interface IAppLifecycleService
{
    bool IsExplicitExit { get; }

    void ShowSettingsWindow(string? page = null);

    /// <summary>Open settings on action-binding page and focus a gesture pattern (e.g. U, D, UL).</summary>
    void ShowGestureBindingEditor(string? pattern = null);

    void ToggleSettingsWindow();

    void ShowWorkstationDashboardWindow();

    void OpenLatestReleasePage();

    Task CheckForUpdatesAsync();

    Task StartCoverUpdateAsync();

    void ExitApplication();
}
