using Xunit;

namespace GestureClip.Tests.App;

public sealed class AppIconTests
{
    [Fact]
    public void App_project_uses_gestureclip_icon_for_exe_and_tray()
    {
        var projectPath = FindRepositoryFile("src", "GestureClip.App", "GestureClip.App.csproj");
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var iconPath = FindRepositoryFile("src", "GestureClip.App", "Assets", "GestureClip.ico");

        var project = File.ReadAllText(projectPath);
        var tray = File.ReadAllText(trayPath);
        var iconInfo = new FileInfo(iconPath);

        Assert.Contains("<ApplicationIcon>Assets\\GestureClip.ico</ApplicationIcon>", project);
        Assert.Contains("Assets\\GestureClip.ico", project);
        Assert.Contains("LoadAppIcon()", tray);
        Assert.DoesNotContain("Icon = SystemIcons.Application", tray);
        Assert.True(iconInfo.Length > 1024);
    }

    [Fact]
    public void App_icon_png_matches_current_bear_clipboard_artwork()
    {
        var iconPngPath = FindRepositoryFile("src", "GestureClip.App", "Assets", "GestureClipIcon.png");

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(iconPngPath)));

        Assert.Equal("049C097BF1630C8A33B4F03F2297655A56B869778CCC4EF67FB6054B685CDF00", hash);
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

