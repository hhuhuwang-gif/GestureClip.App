using Xunit;

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
    public void LowLevelMouseHook_does_not_write_each_mouse_event_at_information_level()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Infrastructure", "Gestures", "LowLevelMouseHook.cs"));

        Assert.Contains("Low-level mouse hook event received", source);
        Assert.Contains("LogDebug", source);
        Assert.DoesNotContain("LogInformation(\r\n            \"Low-level mouse hook event received", source);
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
