namespace GestureClip.Core.Abstractions;

public interface IConfirmationService
{
    bool Confirm(string title, string message);
}
