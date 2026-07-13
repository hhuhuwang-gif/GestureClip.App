namespace GestureClip.Core.Assistant;

public enum AssistantInputKind
{
    None = 0,
    ClipboardText = 1,
    SelectedText = 2
}

public enum AssistantOutputKind
{
    None = 0,
    Preview = 1,
    Clipboard = 2,
    Paste = 3
}

public enum AssistantPrivacyLevel
{
    LocalOnly = 0,
    NetworkOptIn = 1
}
