namespace GestureClip.Core.Abstractions;

public interface IKeyboardInputSender
{
    string? LastStatus { get; }

    void SendShortcut(params ushort[] keys);

    void SendKey(ushort key);
}
