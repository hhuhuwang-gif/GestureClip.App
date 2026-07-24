namespace GestureClip.Core.Abstractions;

public interface IImageOcrService
{
    /// <summary>Returns recognized text or empty string. Never throws.</summary>
    Task<string> RecognizePngBase64Async(string? pngBase64, CancellationToken cancellationToken = default);
}
