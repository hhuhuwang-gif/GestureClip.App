namespace GestureClip.App.ViewModels;

public enum ClipboardOverlayFilter
{
    All,
    Pinned,
    Favorites,
    Text,
    Images,
    Links
}

public enum ClipboardTextTransform
{
    UpperCase,
    LowerCase,
    CompactWhitespace,
    FormatJson,
    MinifyJson,
    UrlDecode
}

public sealed record ClipboardOverlayFilterOption(ClipboardOverlayFilter Filter, string Label);
