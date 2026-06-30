using System.Diagnostics;
using System.IO;
using GestureClip.Core.Abstractions;

namespace GestureClip.Infrastructure.Startup;

public sealed class WindowsStartupService : IStartupService
{
    private const string StartupName = "GestureClip";
    private readonly IStartupRegistry _registry;
    private readonly string _executablePath;

    public WindowsStartupService(IStartupRegistry registry)
        : this(registry, Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "GestureClip.App.exe")
    {
    }

    public WindowsStartupService(IStartupRegistry registry, string executablePath)
    {
        _registry = registry;
        _executablePath = executablePath;
    }

    public bool IsEnabled()
    {
        return string.Equals(_registry.GetValue(StartupName), GetStartupCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        _registry.SetValue(StartupName, GetStartupCommand());
    }

    public void Disable()
    {
        _registry.DeleteValue(StartupName);
    }

    public string GetStartupCommand()
    {
        return _executablePath.Contains(' ')
            ? $"\"{_executablePath}\""
            : _executablePath;
    }

    public bool IsDevelopmentRunMode()
    {
        var fileName = Path.GetFileName(_executablePath);
        return string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
            _executablePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
            _executablePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);
    }
}
