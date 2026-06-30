using System.Windows;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WpfClipboardTextReader : IClipboardTextReader
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WpfClipboardTextReader> _logger;

    public WpfClipboardTextReader(ILogger<WpfClipboardTextReader> logger)
    {
        _dispatcher = Application.Current.Dispatcher;
        _logger = logger;
    }

    public string? TryReadText()
    {
        try
        {
            return _dispatcher.Invoke(() =>
            {
                if (!System.Windows.Clipboard.ContainsText())
                {
                    return null;
                }

                return System.Windows.Clipboard.GetText();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text from clipboard.");
            return null;
        }
    }
}
