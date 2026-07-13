namespace GestureClip.Core.Abstractions;

public interface IQuickActionCenterService
{
    Task ShowAsync();

    Task ToggleAsync();

    Task HideAsync();
}
