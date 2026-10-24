using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using GestureClip.App.Controls;

namespace GestureClip.App;

public partial class SettingsWindow
{
    public void EnableScrollDiagnostics(Action<string> log)
    {
        log($"render tier={RenderCapability.Tier >> 16} "
            + $"mode={RenderOptions.ProcessRenderMode} "
            + $"layered={IsLayeredWindow()}");

        foreach (var tab in MainSettingsTabControl.Items.OfType<TabItem>())
        {
            if (tab.Content is SmoothScrollViewer viewer)
            {
                viewer.DiagnosticLog = message => log($"page={tab.Header} {message}");
                viewer.DiagnosticLog("attached");
            }
        }
    }

    private bool IsLayeredWindow()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        const int GwlExStyle = -20;
        const int WsExLayered = 0x00080000;
        var exStyle = GetWindowLong(handle, GwlExStyle);
        return (exStyle & WsExLayered) != 0;
    }
}
