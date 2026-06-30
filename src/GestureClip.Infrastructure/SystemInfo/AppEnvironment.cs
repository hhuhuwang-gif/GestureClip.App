using System.Diagnostics;
using GestureClip.Core.Abstractions;

namespace GestureClip.Infrastructure.SystemInfo;

public sealed class AppEnvironment : IAppEnvironment
{
    public string ApplicationPath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? AppContext.BaseDirectory;
}
