using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using GestureClip.App.Services;

namespace GestureClip.App;

public partial class MacroPlaybackStatusWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const int NoActivateStyle = 0x08000000;
    private const int ScreenEdgeMargin = 16;
    private readonly MacroRecorderService _recorder;

    public MacroPlaybackStatusWindow(MacroRecorderService recorder)
    {
        _recorder = recorder;
        InitializeComponent();
        DataContext = recorder;
        Left = SystemParameters.WorkArea.Right - Width - ScreenEdgeMargin;
        Top = SystemParameters.WorkArea.Top + ScreenEdgeMargin;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, ExtendedStyleIndex, GetWindowLong(handle, ExtendedStyleIndex) | NoActivateStyle);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _recorder.StopPlayback();
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBlock or System.Windows.Controls.Border) DragMove();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint window, int index, int value);
}
