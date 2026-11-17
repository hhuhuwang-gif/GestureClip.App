using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GestureClip.App.Services;

namespace GestureClip.App.Controls;

public partial class MacroRecorderControl : System.Windows.Controls.UserControl
{
    private MacroRecorderService? _recorder;

    public MacroRecorderControl() => InitializeComponent();

    public void Initialize(MacroRecorderService recorder)
    {
        _recorder = recorder;
        DataContext = recorder;
        if (IsLoaded) AttachHost();
        else Loaded += (_, _) => AttachHost();
    }

    private void AttachHost() => _recorder!.AttachHost(Window.GetWindow(this));
    private async void Record_Click(object sender, RoutedEventArgs e) => await _recorder!.ToggleRecordingAsync();
    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this).WindowState = WindowState.Minimized;
        await Dispatcher.InvokeAsync(() => _recorder!.PlayAsync(), DispatcherPriority.ApplicationIdle).Task.Unwrap();
    }
    private void Stop_Click(object sender, RoutedEventArgs e) => _recorder!.StopPlayback();
    private async void Save_Click(object sender, RoutedEventArgs e) => await _recorder!.SaveRecordingAsync();
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_recorder!.DirectoryPath);
            Process.Start(new ProcessStartInfo(_recorder.DirectoryPath) { UseShellExecute = true });
        }
        catch (Exception error) { _recorder!.ReportError(error); }
    }
}
