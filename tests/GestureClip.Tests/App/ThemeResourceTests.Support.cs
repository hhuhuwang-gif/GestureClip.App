using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{


    private static string ReadAllSettingsViewModelSources()
    {
        var main = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "SettingsViewModel.cs");
        var dir = Path.GetDirectoryName(main)!;
        var files = Directory.GetFiles(dir, "SettingsViewModel*.cs")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string ReadAllSettingsWindowSources()
    {
        var main = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml.cs");
        var dir = Path.GetDirectoryName(main)!;
        var files = Directory.GetFiles(dir, "SettingsWindow*.cs")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
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

    private static string ReadClipboardOverlaySources()
    {
        var window = RepositorySource.ReadClassSources(FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs"));
        var shortcuts = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "Services", "ClipboardShortcutResolver.cs"));
        return string.Join(Environment.NewLine, window, shortcuts);
    }
}
