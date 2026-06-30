using GestureClip.Core.SystemInfo;

namespace GestureClip.Core.Abstractions;

public interface IForegroundAppService
{
    ForegroundAppInfo GetCurrent();
}
