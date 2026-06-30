namespace GestureClip.Core.Gestures;

public sealed class MouseHookEventArgs : EventArgs
{
    public required MouseHookEvent Event { get; init; }

    public bool Suppress { get; set; }
}
