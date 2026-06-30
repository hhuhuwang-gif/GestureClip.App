namespace GestureClip.Core.Abstractions;

public interface ISensitiveContentDetector
{
    bool LooksSensitive(string text);
}
