namespace GestureClip.Core.Privacy;

/// <summary>
/// Per-process smart paste override. Strategy values match SmartPasteStrategy names.
/// </summary>
public sealed record AppSmartPasteRule(
    string ProcessName,
    string Strategy,
    string? Note = null);
