using Xunit;

namespace GestureClip.Tests.WorkerLevel;

public sealed class WorkerLevelUpWindowTests
{
    [Fact]
    public void WorkerLevelUpWindow_does_not_steal_focus_or_mouse_input()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "WorkerLevelUpWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("ShowActivated=\"False\"", xaml);
        Assert.Contains("Focusable=\"False\"", xaml);
        Assert.Contains("IsHitTestVisible=\"False\"", xaml);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml);
        Assert.Contains("Topmost=\"True\"", xaml);
        Assert.Contains("{Binding Header}", xaml);
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

