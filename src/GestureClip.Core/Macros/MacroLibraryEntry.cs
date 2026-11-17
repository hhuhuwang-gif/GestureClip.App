using System.Globalization;

namespace GestureClip.Core.Macros;

public sealed record MacroLibraryEntry(string Path, string Name, DateTimeOffset CreatedAt)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"录制 · {CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}"
        : Name.Trim();
}
