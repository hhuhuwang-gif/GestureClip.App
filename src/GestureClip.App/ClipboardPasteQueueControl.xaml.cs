using System.Windows;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class ClipboardPasteQueueControl : System.Windows.Controls.UserControl
{
    public ClipboardPasteQueueControl() => InitializeComponent();
    private void OnRetry(object sender, RoutedEventArgs args) => (DataContext as ClipboardOverlayViewModel)?.RetryPasteQueue();
    private void OnSkip(object sender, RoutedEventArgs args) => (DataContext as ClipboardOverlayViewModel)?.SkipPasteQueue();
    private void OnCancel(object sender, RoutedEventArgs args) => (DataContext as ClipboardOverlayViewModel)?.ClearPasteQueue();
}
