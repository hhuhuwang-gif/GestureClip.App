using GestureClip.Core.Macros;

namespace GestureClip.Core.Abstractions;

public interface IMacroCapture : IAsyncDisposable, IDisposable
{
    Task StartAsync(nint excludedWindow);
    Task StartPlaybackStopAsync(Action stop);
    Task ExcludeGestureAsync(MacroGestureInterval gesture);
    Task<IReadOnlyList<MacroEvent>> StopAsync();
}

public interface IMacroInputSink
{
    void Send(MacroEvent input);
}
