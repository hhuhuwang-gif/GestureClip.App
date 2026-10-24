using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    [Fact]
    public async Task Injected_right_click_events_can_trigger_gesture_when_not_synthesized_by_app()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, isInjected: true);
        hook.Raise(MouseHookEventType.Move, 0, -30, isInjected: true);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -50, isInjected: true);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Empty(synthesizer.Clicks);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
    }

    [Fact]
    public async Task Injected_events_during_synthesized_right_click_window_are_passed_through()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, isInjected: true);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, 0, isInjected: true);

        Assert.False(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public async Task Expired_tracking_suppresses_next_right_button_up_and_synthesizes_click()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer, maxDurationMs: 10);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, time: DateTimeOffset.UnixEpoch);
        hook.Raise(MouseHookEventType.Move, 1, 1, time: DateTimeOffset.UnixEpoch.AddMilliseconds(20));
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 2, 2, time: DateTimeOffset.UnixEpoch.AddMilliseconds(30));
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal((0, 0), synthesizer.Clicks.Single());
    }

    [Fact]
    public async Task Blacklisted_foreground_process_passes_right_button_events_through()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay, gestureBlacklisted: true);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var move = hook.Raise(MouseHookEventType.Move, 0, -40);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -60);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(move.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Empty(overlay.Events);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Action_exception_records_error_and_leaves_state_idle()
    {
        var hook = new FakeLowLevelMouseHook();
        var service = CreateService(hook, new ThrowingMouseGestureActionExecutor());
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);

        await WaitForAsync(() => service.Diagnostics.LastError is not null);

        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Contains("boom", service.Diagnostics.LastError);
    }

    [Fact]
    public async Task Consecutive_gestures_do_not_leave_state_stuck()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        for (var i = 0; i < 50; i++)
        {
            var offset = i * 5;
            hook.Raise(MouseHookEventType.RightButtonDown, offset, offset);
            hook.Raise(MouseHookEventType.Move, offset, offset - 30);
            hook.Raise(MouseHookEventType.RightButtonUp, offset, offset - 50);
        }

        await WaitForAsync(() => executor.Actions.Count == 50);

        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Equal(50, executor.Actions.Count);
    }

    [Fact]
    public async Task Safety_timeout_resets_state_and_suppresses_next_right_button_up()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer, maxDurationMs: 100);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 10, 10, time: DateTimeOffset.UnixEpoch);
        hook.Raise(MouseHookEventType.Move, 12, 12, time: DateTimeOffset.UnixEpoch.AddMilliseconds(700));
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 12, 12, time: DateTimeOffset.UnixEpoch.AddMilliseconds(720));

        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Equal((10, 10), synthesizer.Clicks.Single());
    }

    [Fact]
    public async Task Non_injected_events_are_not_ignored_during_synthesized_right_click_window()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, isInjected: false);
        hook.Raise(MouseHookEventType.Move, 0, -30, isInjected: false);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -50, isInjected: false);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
    }

    [Fact]
    public async Task Replayed_click_runs_outside_low_level_hook_callback_thread()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new ThreadCapturingClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);
        var callbackThreadId = Environment.CurrentManagedThreadId;

        hook.Raise(MouseHookEventType.RightButtonDown, 20, 30);
        hook.Raise(MouseHookEventType.RightButtonUp, 20, 30);
        await synthesizer.Clicked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotEqual(callbackThreadId, synthesizer.ThreadId);
    }

    private sealed class ThreadCapturingClickSynthesizer : IRightClickSynthesizer
    {
        public TaskCompletionSource Clicked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ThreadId { get; private set; }

        public void SynthesizeRightClick(int x, int y) => SynthesizeClick(GestureTriggerButton.Right, x, y);

        public void SynthesizeClick(GestureTriggerButton button, int x, int y)
        {
            ThreadId = Environment.CurrentManagedThreadId;
            Clicked.TrySetResult();
        }

        public void SynthesizeWheel(int delta, int x, int y)
        {
        }
    }
}
