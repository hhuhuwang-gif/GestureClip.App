using GestureClip.Infrastructure.Startup;
using Xunit;

namespace GestureClip.Tests.Startup;

public sealed class WindowsStartupServiceTests
{
    [Fact]
    public void Enable_writes_quoted_startup_command_and_disable_removes_it()
    {
        var registry = new FakeStartupRegistry();
        var service = new WindowsStartupService(registry, @"C:\Program Files\GestureClip\GestureClip.App.exe");

        service.Enable();

        Assert.True(service.IsEnabled());
        Assert.Equal("\"C:\\Program Files\\GestureClip\\GestureClip.App.exe\"", registry.Value);
        Assert.Equal(registry.Value, service.GetStartupCommand());

        service.Disable();

        Assert.False(service.IsEnabled());
        Assert.Null(registry.Value);
    }

    [Fact]
    public void GetStartupCommand_always_quotes_full_exe_path()
    {
        var service = new WindowsStartupService(new FakeStartupRegistry(), @"C:\GestureClip\GestureClip.exe");

        Assert.Equal("\"C:\\GestureClip\\GestureClip.exe\"", service.GetStartupCommand());
    }

    [Fact]
    public void Enable_throws_for_dotnet_development_host()
    {
        var service = new WindowsStartupService(new FakeStartupRegistry(), @"C:\Program Files\dotnet\dotnet.exe");

        Assert.Throws<InvalidOperationException>(() => service.Enable());
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", true)]
    [InlineData(@"C:\repo\GestureClip\src\GestureClip.App\bin\Debug\net8.0-windows\GestureClip.App.exe", true)]
    [InlineData(@"C:\Program Files\GestureClip\GestureClip.App.exe", false)]
    public void IsDevelopmentRunMode_detects_development_paths(string executablePath, bool expected)
    {
        var service = new WindowsStartupService(new FakeStartupRegistry(), executablePath);

        Assert.Equal(expected, service.IsDevelopmentRunMode());
    }

    private sealed class FakeStartupRegistry : IStartupRegistry
    {
        public string? Value { get; private set; }

        public string? GetValue(string name) => Value;

        public void SetValue(string name, string value) => Value = value;

        public void DeleteValue(string name) => Value = null;
    }
}
