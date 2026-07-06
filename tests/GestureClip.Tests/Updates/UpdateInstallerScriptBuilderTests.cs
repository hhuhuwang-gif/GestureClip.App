using GestureClip.Infrastructure.Updates;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class UpdateInstallerScriptBuilderTests
{
    [Fact]
    public void Build_creates_cover_install_script_that_preserves_local_data_and_restarts_app()
    {
        var script = UpdateInstallerScriptBuilder.Build(
            sourceDirectory: @"C:\Temp\GestureClipUpdate",
            installDirectory: @"C:\Apps\GestureClip",
            executableName: "GestureClip.exe");

        Assert.Contains("robocopy", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C:\\Temp\\GestureClipUpdate", script);
        Assert.Contains("C:\\Apps\\GestureClip", script);
        Assert.Contains("/XF gestureclip.db gestureclip.db-shm gestureclip.db-wal", script);
        Assert.Contains("/XD logs", script);
        Assert.Contains("set \"EXE=GestureClip.exe\"", script);
        Assert.Contains("start \"\" \"%DEST%\\%EXE%\"", script);
    }
}
