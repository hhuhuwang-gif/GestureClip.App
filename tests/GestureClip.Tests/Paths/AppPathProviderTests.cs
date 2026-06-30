using GestureClip.Infrastructure.Paths;
using Xunit;

namespace GestureClip.Tests.Paths;

public sealed class AppPathProviderTests
{
    [Fact]
    public void Default_paths_are_under_local_app_data_gestureclip_directory()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), "LocalAppData");
        var paths = new AppPathProvider(localAppData);

        Assert.Equal(Path.Combine(localAppData, "GestureClip"), paths.RootDirectory);
        Assert.Equal(Path.Combine(localAppData, "GestureClip", "gestureclip.db"), paths.DatabasePath);
        Assert.Equal(Path.Combine(localAppData, "GestureClip", "logs"), paths.LogDirectory);
    }
}
