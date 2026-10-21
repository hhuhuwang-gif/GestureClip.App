using GestureClip.Core.Abstractions;

namespace GestureClip.Features.Clipboard;

public sealed record PlainTextPasteDependencies(
    IClipboardService ClipboardService,
    IClipboardTextReader TextReader,
    IClipboardWriter Writer);
