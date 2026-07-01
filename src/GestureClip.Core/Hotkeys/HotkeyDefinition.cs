namespace GestureClip.Core.Hotkeys;

public sealed record HotkeyDefinition(uint Modifiers, uint VirtualKey, string DisplayText)
{
    public const string DefaultOpenClipboardOverlay = "Ctrl + `";

    public static HotkeyDefinition ParseOrDefault(string? text)
    {
        return TryParse(text, out var hotkey)
            ? hotkey
            : new HotkeyDefinition(HotkeyModifier.Control, HotkeyVirtualKey.Oem3, DefaultOpenClipboardOverlay);
    }

    public static bool TryParse(string? text, out HotkeyDefinition hotkey)
    {
        hotkey = new HotkeyDefinition(HotkeyModifier.Control, HotkeyVirtualKey.Oem3, DefaultOpenClipboardOverlay);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        if (parts.Length < 2)
        {
            return false;
        }

        uint modifiers = 0;
        uint? virtualKey = null;
        var displayParts = new List<string>();
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifier.Control;
                displayParts.Add("Ctrl");
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifier.Alt;
                displayParts.Add("Alt");
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifier.Shift;
                displayParts.Add("Shift");
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifier.Win;
                displayParts.Add("Win");
                continue;
            }

            if (virtualKey is not null)
            {
                return false;
            }

            if (part is "`" or "Backtick" or "Oem3")
            {
                virtualKey = HotkeyVirtualKey.Oem3;
                displayParts.Add("`");
                continue;
            }

            if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
            {
                var key = char.ToUpperInvariant(part[0]);
                virtualKey = key;
                displayParts.Add(key.ToString());
                continue;
            }

            return false;
        }

        if (modifiers == 0 || virtualKey is null)
        {
            return false;
        }

        hotkey = new HotkeyDefinition(modifiers, virtualKey.Value, string.Join(" + ", displayParts));
        return true;
    }
}

public static class HotkeyModifier
{
    public const uint Alt = 0x0001;
    public const uint Control = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win = 0x0008;
}

public static class HotkeyVirtualKey
{
    public const uint Oem3 = 0xC0;
}
