using GestureClip.Core.SystemInfo;

namespace GestureClip.Core.Abstractions;

public interface ISystemPermissionService
{
    PermissionStatus GetCurrentStatus();
}
