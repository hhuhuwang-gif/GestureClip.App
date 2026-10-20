using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    [Fact]
    public async Task Application_profile_uses_process_captured_on_down_even_after_foreground_changes()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        await profiles.SaveAsync([
            new("excel", true, [new("U", false, BuiltInGestureAction.Redo)]),
            new("chrome", true, [new("U", false, BuiltInGestureAction.SelectAll)])], default);
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var foreground = new FakeForegroundAppService { Current = new("EXCEL.exe", "Sheet") };
        var provider = new GesturePresetProvider(settings, profiles);
        var service = CreateService(hook, executor, showOverlay: false, presetProvider: provider, foregroundApp: foreground);
        await service.StartAsync(default);
        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        foreground.Current = new("chrome.exe", "Browser");
        hook.Raise(MouseHookEventType.Move, 0, -40);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -60);
        await WaitForAsync(() => !executor.Actions.IsEmpty);
        Assert.Equal(BuiltInGestureAction.Redo, Assert.Single(executor.Actions));
        Assert.Equal("EXCEL.exe", Assert.Single(executor.Contexts).TargetProcessName);
        await service.StopAsync(default);
    }
}
