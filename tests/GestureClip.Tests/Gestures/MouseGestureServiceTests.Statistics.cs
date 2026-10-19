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
    public async Task Gesture_active_records_gesture_count_after_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(hook, executor, dashboard: dashboard, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);
        await WaitForAsync(() => dashboard.GestureCount == 1);

        Assert.Equal(1, dashboard.GestureCount);
    }

    [Fact]
    public async Task Slow_stats_and_worker_level_do_not_delay_overlay_hide_after_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var dashboard = new FakeWorkstationDashboardService { RecordGestureDelay = TimeSpan.FromMilliseconds(500) };
        var workerLevel = new FakeWorkerLevelService { RecordDelay = TimeSpan.FromMilliseconds(500) };
        var service = CreateService(
            hook,
            executor,
            overlay: overlay,
            dashboard: dashboard,
            workerLevel: workerLevel);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);
        await Task.Delay(120);

        Assert.Contains("hide", overlay.Events);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Gesture_action_success_records_worker_level_xp()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var workerLevel = new FakeWorkerLevelService();
        var service = CreateService(hook, executor, showOverlay: false, workerLevel: workerLevel);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => workerLevel.RecordedActions.Count == 1);

        Assert.Equal(BuiltInGestureAction.Copy, workerLevel.RecordedActions.Single());
        Assert.True(workerLevel.LastGestureSuccess);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Worker_level_exception_does_not_break_gesture_state()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var workerLevel = new FakeWorkerLevelService { ThrowOnRecord = true };
        var service = CreateService(hook, executor, showOverlay: false, workerLevel: workerLevel);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Gesture_level_up_requests_level_up_popup_after_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var workerLevel = new FakeWorkerLevelService { LeveledUp = true };
        var levelUp = new FakeWorkerLevelUpService();
        var service = CreateService(hook, executor, showOverlay: false, workerLevel: workerLevel, levelUp: levelUp);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => levelUp.ShowCount == 1);

        Assert.Equal(1, levelUp.ShowCount);
    }
}
