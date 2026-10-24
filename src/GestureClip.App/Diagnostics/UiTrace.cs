using System.Diagnostics;

namespace GestureClip.App.Diagnostics;

/// <summary>
/// Opt-in UI timing trace, switched on with <c>--trace-ui</c> (same idea as <c>--trace-scroll</c>).
/// Timing lines are kept out of the normal log because they are noise for end users, but they are
/// the only way to tell an inflated visual tree apart from a slow layout pass when someone reports
/// "opening settings takes ages" or "switching pages lags".
/// </summary>
internal static class UiTrace
{
    public static bool Enabled { get; private set; }

    public static Action<string>? Sink { get; private set; }

    public static long Restart()
    {
        return Stopwatch.GetTimestamp();
    }

    public static long ElapsedMillisecondsSince(long startedAt)
    {
        return (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
    }

    public static void Report(string message)
    {
        Sink?.Invoke(message);
    }

    public static void Configure(bool enabled, Action<string>? sink)
    {
        Enabled = enabled;
        Sink = enabled ? sink : null;
    }
}
