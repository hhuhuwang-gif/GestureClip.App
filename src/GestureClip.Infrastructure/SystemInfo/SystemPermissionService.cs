using System.Security.Principal;
using GestureClip.Core.Abstractions;
using GestureClip.Core.SystemInfo;

namespace GestureClip.Infrastructure.SystemInfo;

public sealed class SystemPermissionService : ISystemPermissionService
{
    public PermissionStatus GetCurrentStatus()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(WindowsBuiltInRole.Administrator)
            ? PermissionStatus.Administrator
            : PermissionStatus.Normal;
    }
}
