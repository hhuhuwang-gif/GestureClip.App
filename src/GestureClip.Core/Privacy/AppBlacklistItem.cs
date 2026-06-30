namespace GestureClip.Core.Privacy;

public sealed record AppBlacklistItem(
    Guid Id,
    string ProcessName,
    bool BlockClipboard,
    bool BlockGesture,
    string? Reason);
