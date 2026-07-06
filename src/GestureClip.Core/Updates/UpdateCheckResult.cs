namespace GestureClip.Core.Updates;

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseUrl,
    string ReleaseNotes,
    bool IsUpdateAvailable);
