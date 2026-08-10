namespace GestureClip.Core.Abstractions;

public interface IClipboardOverlayService
{
    Task ShowAsync();

    Task ToggleAsync();

    Task RefreshAsync();

    /// <summary>
    /// Pre-creates the overlay window (off the critical path, at startup) so the
    /// first hotkey/gesture open is instant instead of paying XAML parse cost.
    /// Must be called on the UI thread.
    /// </summary>
    Task WarmUpAsync();
}
