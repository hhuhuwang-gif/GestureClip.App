namespace GestureClip.Core.Hotkeys;

public enum HotkeyRegistrationState
{
    NotStarted = 0,
    Registered = 1,
    Failed = 2
}

public sealed record HotkeyStatus(
    HotkeyRegistrationState State,
    string DisplayText,
    int? Win32Error = null);
