namespace GestureClip.Core.Clipboard;

public enum ClipboardContentFilter
{
    All,
    Pinned,
    Favorites,
    Text,
    Images,
    /// <summary>Text records that look like URLs (contain "://" or start with www.).</summary>
    Links
}
