using System.Windows;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private void OpenGestureProfiles_Click(object sender, RoutedEventArgs args) =>
        _appLifecycleService.ShowGestureProfilesWindow(this);
}
