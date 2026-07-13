using GestureClip.Infrastructure.Updates;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class ReleasePackageSelectorTests
{
    [Fact]
    public void Select_prefers_setup_exe_over_setup_zip_and_portable()
    {
        var assets = new[]
        {
            Asset("GestureClip-v0.6.16-beta-win-x64.zip", "https://example/portable.zip"),
            Asset("GestureClip-Setup-v0.6.16-beta-win-x64.zip", "https://example/setup.zip"),
            Asset("GestureClip-Setup-v0.6.16-beta-win-x64.exe", "https://example/setup.exe"),
        };

        var selected = ReleasePackageSelector.Select(assets);

        Assert.NotNull(selected);
        Assert.Equal(ReleasePackageSelector.PackageKind.SetupExe, selected!.Kind);
        Assert.EndsWith(".exe", selected.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_prefers_setup_zip_over_portable()
    {
        var assets = new[]
        {
            Asset("GestureClip-v0.6.16-beta-win-x64.zip", "https://example/portable.zip"),
            Asset("GestureClip-Setup-v0.6.16-beta-win-x64.zip", "https://example/setup.zip"),
        };

        var selected = ReleasePackageSelector.Select(assets);

        Assert.NotNull(selected);
        Assert.Equal(ReleasePackageSelector.PackageKind.SetupZip, selected!.Kind);
        Assert.Contains("Setup", selected.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_falls_back_to_portable_zip()
    {
        var assets = new[]
        {
            Asset("notes.txt", "https://example/notes.txt"),
            Asset("GestureClip-v0.6.16-beta-win-x64.zip", "https://example/portable.zip"),
        };

        var selected = ReleasePackageSelector.Select(assets);

        Assert.NotNull(selected);
        Assert.Equal(ReleasePackageSelector.PackageKind.PortableZip, selected!.Kind);
    }

    [Fact]
    public void Select_returns_null_when_no_windows_package()
    {
        var assets = new[]
        {
            Asset("README.md", "https://example/readme"),
            Asset("Source.tar.gz", "https://example/src"),
        };

        Assert.Null(ReleasePackageSelector.Select(assets));
    }

    private static GitHubReleaseUpdateCheckService.GitHubReleaseAsset Asset(string name, string url) =>
        new(name, url);
}
