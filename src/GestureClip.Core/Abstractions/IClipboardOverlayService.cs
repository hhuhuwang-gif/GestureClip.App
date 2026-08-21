namespace GestureClip.Core.Abstractions;

public interface IClipboardOverlayService
{
    Task ShowAsync();

    Task ToggleAsync();

    Task RefreshAsync();

    Task WarmUpAsync() => Task.CompletedTask;
}
