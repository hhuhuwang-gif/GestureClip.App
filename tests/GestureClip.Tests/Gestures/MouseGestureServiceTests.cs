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
    public async Task StartAsync_and_StopAsync_are_idempotent()
    {
        var hook = new FakeLowLevelMouseHook();
        var service = CreateService(hook);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, hook.StartCount);
        Assert.Equal(1, hook.StopCount);
    }

    [Fact]
    public async Task StopAsync_clears_enabled_state_for_fast_restart()
    {
        var hook = new FakeLowLevelMouseHook();
        var service = CreateService(hook);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.False(service.IsEnabled);

        await service.StartAsync(CancellationToken.None);

        Assert.True(service.IsEnabled);
        Assert.Equal(2, hook.StartCount);
    }

    [Fact]
    public async Task StopAsync_resets_tracking_state()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        await service.StopAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await Task.Delay(50);

        Assert.True(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(synthesizer.Clicks);
    }

    [Fact]
    public async Task Environment_variable_disables_starting_hook()
    {
        var previous = Environment.GetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES");
        Environment.SetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES", "1");
        try
        {
            var hook = new FakeLowLevelMouseHook();
            var service = CreateService(hook);

            await service.StartAsync(CancellationToken.None);

            Assert.Equal(0, hook.StartCount);
            Assert.False(service.IsEnabled);
            Assert.Equal("被环境变量禁用", service.Diagnostics.HookStatus);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES", previous);
        }
    }

    [Fact]
    public async Task Disabled_service_passes_events_through()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, enabled: false);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var move = hook.Raise(MouseHookEventType.Move, 0, -40);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -60);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(move.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    private static MouseGestureService CreateService(
        FakeLowLevelMouseHook hook,
        IMouseGestureActionExecutor? executor = null,
        IRightClickSynthesizer? synthesizer = null,
        FakeGestureOverlayService? overlay = null,
        bool enabled = true,
        int maxDurationMs = 2000,
        bool showOverlay = true,
        bool debugEnabled = false,
        bool gestureBlacklisted = false,
        bool leftButtonEnabled = false,
        bool rightButtonEnabled = true,
        bool middleButtonEnabled = false,
        bool xButton1Enabled = false,
        bool xButton2Enabled = false,
        FakeWorkstationDashboardService? dashboard = null,
        FakeWorkerLevelService? workerLevel = null,
        FakeWorkerLevelUpService? levelUp = null,
        IGesturePresetProvider? presetProvider = null,
        FakeForegroundAppService? foregroundApp = null,
        FakeMouseButtonStateProvider? buttonState = null)
    {
        var resolvedPresetProvider = presetProvider ?? new GesturePresetProvider();
        return new MouseGestureService(
            new MouseGestureDependencies
            {
                MouseHook = hook,
                Recognizer = new DirectionGestureRecognizer(),
                ActionExecutor = executor ?? new FakeMouseGestureActionExecutor(),
                RightClickSynthesizer = synthesizer ?? new FakeRightClickSynthesizer(),
                MouseButtonStateProvider = buttonState ?? new FakeMouseButtonStateProvider(),
                SettingsProvider = new FakeGestureSettingsProvider(enabled, maxDurationMs, showOverlay, debugEnabled, leftButtonEnabled, rightButtonEnabled, middleButtonEnabled, xButton1Enabled, xButton2Enabled),
                PresetProvider = resolvedPresetProvider,
                HudInfoProvider = new GestureHudInfoProvider(resolvedPresetProvider),
                GestureOverlayService = overlay ?? new FakeGestureOverlayService(),
                ForegroundAppService = foregroundApp ?? new FakeForegroundAppService(),
                AppBlacklistService = new FakeAppBlacklistService { GestureBlocked = gestureBlacklisted },
                WorkstationDashboardService = dashboard ?? new FakeWorkstationDashboardService(),
                WorkerLevelService = workerLevel ?? new FakeWorkerLevelService(),
                WorkerLevelUpService = levelUp ?? new FakeWorkerLevelUpService()
            },
            NullLogger<MouseGestureService>.Instance);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }
}
