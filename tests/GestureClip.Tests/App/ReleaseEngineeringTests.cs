using Xunit;

namespace GestureClip.Tests.App;

public sealed class ReleaseEngineeringTests
{
    [Fact]
    public void App_project_uses_v060_beta_version_metadata()
    {
        var project = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "GestureClip.App.csproj"));

        Assert.Contains("<Version>0.6.0-beta</Version>", project);
        Assert.Contains("<FileVersion>0.6.0.0</FileVersion>", project);
        Assert.Contains("<AssemblyVersion>0.6.0.0</AssemblyVersion>", project);
        Assert.Contains("<InformationalVersion>0.6.0 Beta</InformationalVersion>", project);
    }

    [Fact]
    public void Startup_log_includes_human_readable_version()
    {
        var app = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs"));

        Assert.Contains("AssemblyInformationalVersionAttribute", app);
        Assert.Contains("GestureClip v{AppVersion} started.", app);
    }

    [Fact]
    public void Publish_script_creates_full_and_update_beta_packages()
    {
        var script = File.ReadAllText(FindRepositoryFile("scripts", "publish-win-x64.ps1"));

        Assert.Contains("GestureClip-v$packageVersion-win-x64.zip", script);
        Assert.Contains("GestureClip-v$packageVersion-update-win-x64.zip", script);
        Assert.Contains("UPDATE.md", script);
        Assert.Contains("HELP.md", script);
        Assert.Contains("BETA_TEST.md", script);
        Assert.Contains("KNOWN_ISSUES.md", script);
        Assert.Contains("CHANGELOG.md", script);
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("SHA256SUMS.txt", script);
        Assert.Contains("Remove-Item (Join-Path $output \"gestureclip.db\")", script);
        Assert.Contains("Remove-Item (Join-Path $output \"logs\")", script);
    }

    [Fact]
    public void Release_documents_describe_cover_update_and_local_data()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));
        var update = File.ReadAllText(FindRepositoryFile("UPDATE.md"));
        var help = File.ReadAllText(FindRepositoryFile("HELP.md"));
        var betaTest = File.ReadAllText(FindRepositoryFile("BETA_TEST.md"));
        var knownIssues = File.ReadAllText(FindRepositoryFile("KNOWN_ISSUES.md"));
        var changelog = File.ReadAllText(FindRepositoryFile("CHANGELOG.md"));
        var releaseDraft = File.ReadAllText(FindRepositoryFile("docs", "github-release-v0.6.0-beta.md"));

        Assert.Contains("v0.6.0 Beta", readme);
        Assert.Contains("GestureClip-v0.6.0-beta-win-x64.zip", readme);
        Assert.Contains("%LOCALAPPDATA%\\GestureClip", update);
        Assert.Contains("覆盖更新", update);
        Assert.Contains("导出诊断包", help);
        Assert.Contains("公测检查清单", betaTest);
        Assert.Contains("SmartScreen", knownIssues);
        Assert.Contains("GestureClip v0.6.0 Beta", changelog);
        Assert.Contains("GestureClip-v0.6.0-beta-update-win-x64.zip", releaseDraft);
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

