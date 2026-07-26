namespace GestureClip.Core.Abstractions;

/// <summary>
/// Reports how long the user has had no keyboard/mouse input, system-wide.
/// Only the idle duration is exposed — no window, app, or content information.
/// </summary>
public interface IUserIdleProvider
{
    TimeSpan GetIdleDuration();
}
