namespace GestureClip.Infrastructure.Updates;

/// <summary>
/// Picks the best GitHub release asset for install/update:
/// 1) Windows Setup .exe (Inno etc.)
/// 2) Setup zip (Setup.cmd + payload)
/// 3) Portable app zip (legacy cover update)
/// </summary>
public static class ReleasePackageSelector
{
    public const string SetupExeMarker = "setup";
    public const string SetupZipSuffix = "-win-x64.zip";
    public const string PortableZipSuffix = "-win-x64.zip";

    public enum PackageKind
    {
        None = 0,
        SetupExe = 1,
        SetupZip = 2,
        PortableZip = 3
    }

    public sealed record SelectedPackage(
        PackageKind Kind,
        string Name,
        string BrowserDownloadUrl);

    public static SelectedPackage? Select(
        IEnumerable<GitHubReleaseUpdateCheckService.GitHubReleaseAsset> assets)
    {
        var list = assets?
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
            .ToArray() ?? [];

        // 1) Dedicated setup executable
        var setupExe = list.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains(SetupExeMarker, StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("GestureClip", StringComparison.OrdinalIgnoreCase));
        if (setupExe is not null)
        {
            return new SelectedPackage(PackageKind.SetupExe, setupExe.Name, setupExe.BrowserDownloadUrl);
        }

        // 2) Setup zip (contains Setup.cmd)
        var setupZip = list.FirstOrDefault(a =>
            a.Name.EndsWith(SetupZipSuffix, StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains(SetupExeMarker, StringComparison.OrdinalIgnoreCase));
        if (setupZip is not null)
        {
            return new SelectedPackage(PackageKind.SetupZip, setupZip.Name, setupZip.BrowserDownloadUrl);
        }

        // 3) Portable full app zip (legacy)
        var portable = list.FirstOrDefault(a =>
            a.Name.EndsWith(PortableZipSuffix, StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("GestureClip", StringComparison.OrdinalIgnoreCase) &&
            !a.Name.Contains(SetupExeMarker, StringComparison.OrdinalIgnoreCase));
        if (portable is not null)
        {
            return new SelectedPackage(PackageKind.PortableZip, portable.Name, portable.BrowserDownloadUrl);
        }

        return null;
    }
}
