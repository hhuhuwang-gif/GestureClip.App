namespace GestureClip.Core.Abstractions;

public interface IPlainTextPasteService
{
    /// <summary>
    /// Rewrite system clipboard as plain Unicode text (no rich formats) and send Ctrl+V.
    /// </summary>
    Task PastePlainTextAsync(CancellationToken cancellationToken = default);
}
