using System.Text.RegularExpressions;

namespace GestureClip.Infrastructure.Updates;

public static class UpdateVersionComparer
{
    public static bool IsNewerRelease(string currentVersion, string latestTag)
    {
        var current = ParseVersionParts(currentVersion);
        var latest = ParseVersionParts(latestTag);
        if (current is null || latest is null)
        {
            return false;
        }

        return latest.CompareTo(current) > 0;
    }

    private static Version? ParseVersionParts(string value)
    {
        var match = Regex.Match(value, @"(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)");
        if (!match.Success)
        {
            return null;
        }

        return new Version(
            int.Parse(match.Groups["major"].Value),
            int.Parse(match.Groups["minor"].Value),
            int.Parse(match.Groups["patch"].Value));
    }
}
