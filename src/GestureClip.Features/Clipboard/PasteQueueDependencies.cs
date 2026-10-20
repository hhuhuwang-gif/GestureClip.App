using GestureClip.Core.Abstractions;

namespace GestureClip.Features.Clipboard;

public sealed record PasteQueueDependencies(
    IHotkeyRegistrar Registrar,
    IClipboardService ClipboardService,
    IPasteTargetService TargetService);
