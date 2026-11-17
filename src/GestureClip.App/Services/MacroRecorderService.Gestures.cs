using System.Windows;
using GestureClip.Core.Gestures;
using GestureClip.Core.Macros;

namespace GestureClip.App.Services;

public sealed partial class MacroRecorderService
{
    public Task ExecuteGestureAsync(BuiltInGestureAction action, GestureExecutionContext context, CancellationToken token)
        => System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ExecuteGestureCoreAsync(action, context),
            System.Windows.Threading.DispatcherPriority.Normal, token).Task.Unwrap();

    private async Task ExecuteGestureCoreAsync(BuiltInGestureAction action, GestureExecutionContext context)
    {
        await EnsureGestureHostAsync();
        if (_transition && !_playing && !_recording) await _operation;
        if (action == BuiltInGestureAction.MacroToggleRecording)
        {
            if (_recording)
                await capture.ExcludeGestureAsync(new MacroGestureInterval(context.GestureStartedAt, context.GestureEndedAt));
            await ToggleRecordingAsync();
            return;
        }
        if (_playing) { StopPlayback(); return; }
        await PlayAsync();
    }

    private async Task EnsureGestureHostAsync()
    {
        if (_host is not null) return;
        _window ??= new MacroRecorderWindow(this) { ShowActivated = false, WindowState = WindowState.Minimized };
        _window.Closed += GestureWindowClosed;
        _window.Show();
        AttachHost(_window);
        await _operation;
    }

    private void GestureWindowClosed(object? sender, EventArgs e) => _window = null;
}
