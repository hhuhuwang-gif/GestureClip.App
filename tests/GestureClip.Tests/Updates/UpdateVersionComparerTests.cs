using GestureClip.Infrastructure.Updates;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class UpdateVersionComparerTests
{
    [Theory]
    [InlineData("0.6.2 Beta", "v0.6.3-beta", true)]
    [InlineData("0.6.2 Beta", "v0.6.2-beta", false)]
    [InlineData("0.6.2 Beta", "v0.6.1-beta", false)]
    [InlineData("0.6.2", "v0.6.2-beta", false)]
    public void IsNewerRelease_compares_current_version_with_github_tag(string currentVersion, string latestTag, bool expected)
    {
        Assert.Equal(expected, UpdateVersionComparer.IsNewerRelease(currentVersion, latestTag));
    }
}
