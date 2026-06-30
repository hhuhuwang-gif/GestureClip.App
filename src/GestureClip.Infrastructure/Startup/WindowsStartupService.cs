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
        if (IsDotNetHost())
        {
            throw new InvalidOperationException("Cannot enable startup while running under dotnet.exe.");
        }

        _registry.SetValue(StartupName, GetStartupCommand());
    }

    public void Disable()
    {
        _registry.DeleteValue(StartupName);
    }

    public string GetStartupCommand()
    {
        return $"\"{_executablePath}\"";
    }

    public bool IsDevelopmentRunMode()
    {
        var fileName = Path.GetFileName(_executablePath);
        return IsDotNetHost() ||
            _executablePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
            _executablePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDotNetHost()
    {
        return string.Equals(Path.GetFileName(_executablePath), "dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }
}
