namespace GestureClip.Core.Abstractions;

public interface IClipboardHashService
{
    string ComputeHash(string text);

    string ComputePlainTextHash(string text);
}
