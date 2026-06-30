namespace GestureClip.Core.Clipboard;

public sealed record ClipboardCapture(
    string Text,
    string? SourceApp,
    string? SourceProcess,
    DateTimeOffset CapturedAt);
