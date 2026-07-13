namespace GestureClip.Core.Abstractions;

/// <summary>
/// Decides whether the current UI context should skip clipboard history capture (password fields, etc.).
/// </summary>
public interface ISensitiveCaptureGate
{
    bool ShouldSkipCapture(string? sourceProcess, string? sourceAppOrTitle);
}
