namespace GestureClip.App.Services;

public sealed record ClipboardPasteRequest(bool KeepOverlayOpen, bool PlainText, int? Index = null);

public sealed class ClipboardPasteCoordinator(Func<ClipboardPasteRequest, Task> paste)
{
    private Task? _inFlight;

    public Task PasteAsync(ClipboardPasteRequest request)
    {
        if (_inFlight is { IsCompleted: false } pending)
        {
            return pending;
        }
        return _inFlight = paste(request);
    }
}
