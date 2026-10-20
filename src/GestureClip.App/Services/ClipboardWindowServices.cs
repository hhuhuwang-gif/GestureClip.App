using GestureClip.Core.Abstractions;

namespace GestureClip.App.Services;

public sealed record ClipboardWindowServices(IConfirmationService Confirmation, IPasteTargetService PasteTarget);
