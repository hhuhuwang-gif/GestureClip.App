using Xunit;
using GestureClip.Infrastructure.Gestures;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestureClip.Tests.Gestures;

public sealed class LowLevelMouseHookSourceTests
{
    [Fact]
    public void LowLevelMouseHook_uses_module_handle_when_installing_global_hook()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Infrastructure", "Gestures", "LowLevelMouseHook.cs"));

        Assert.Contains("GetModuleHandle", source);
        Assert.DoesNotContain("_hookProc,\r\n                IntPtr.Zero,\r\n                0", source);
    }

    [Fact]
    public void LowLevelMouseHook_runs_on_a_dedicated_native_message_loop_thread()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Infrastructure", "Gestures", "LowLevelMouseHook.cs"));
        var stateSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "GestureClip.Infrastructure",
            "Gestures",
            "LowLevelMouseHook.State.cs"));

        Assert.Contains("GestureClip.MouseHook", source);
        Assert.Contains("RunMessageLoop(state)", source);
        Assert.Contains("PostThreadMessage", stateSource);
        Assert.DoesNotContain("Application.Current.Dispatcher", source);
    }

    [Fact]
    public void LowLevelMouseHook_does_not_write_each_mouse_event_at_information_level()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "GestureClip.Infrastructure",
            "Gestures",
            "LowLevelMouseHook.Callback.cs"));

        Assert.Contains("Low-level mouse hook event received", source);
        Assert.Contains("LogDebug", source);
        Assert.DoesNotContain("LogInformation(\r\n            \"Low-level mouse hook event received", source);
    }

    [Fact]
    public async Task LowLevelMouseHook_repeated_start_and_stop_complete_the_native_hook_lifecycle()
    {
        using var hook = new LowLevelMouseHook(NullLogger<LowLevelMouseHook>.Instance);

        for (var cycle = 0; cycle < 20; cycle++)
        {
            await hook.StartAsync(CancellationToken.None);
            Assert.True(hook.IsRunning);

            await hook.StopAsync(CancellationToken.None);
            Assert.False(hook.IsRunning);
        }
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
