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
