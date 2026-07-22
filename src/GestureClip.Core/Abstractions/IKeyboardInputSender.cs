namespace GestureClip.Core.Abstractions;

public interface IKeyboardInputSender
{
    string? LastStatus { get; }

    void SendShortcut(params ushort[] keys);

    void SendKey(ushort key);

    /// <summary>
    /// Hardened Ctrl+V for paste gestures / hotkeys.
    /// Prefer this over SendShortcut(Ctrl, V) so the target HWND can be restored.
    /// Default implementation falls back to SendShortcut for test fakes.
    /// </summary>
    Task<bool> SendPasteAsync(nint preferredTargetWindow = 0, CancellationToken cancellationToken = default)
    {
        // VkControl=0x11, VkV=0x56 — keep numeric so Core has no Win32 dependency.
        SendShortcut(0x11, 0x56);
        return Task.FromResult(true);
    }

    /// <summary>Human-readable hint when the last paste injection likely failed (UIPI / elevated target).</summary>
    string? TryGetLastPasteFailureHint() => null;
}
