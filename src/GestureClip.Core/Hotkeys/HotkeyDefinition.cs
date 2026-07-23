namespace GestureClip.Core.Hotkeys;

public sealed record HotkeyDefinition(uint Modifiers, uint VirtualKey, string DisplayText)
{
    public const string DefaultOpenClipboardOverlay = "Ctrl + `";
    public const string FallbackOpenClipboardOverlay = "Ctrl + Alt + V";
    public const string DefaultOpenQuickActionCenter = "Ctrl + Shift + Q";
    public const string FallbackOpenQuickActionCenter = "Ctrl + Alt + Q";
    public const string DefaultPastePlainText = "Ctrl + Shift + V";
    public const string FallbackPastePlainText = "Ctrl + Alt + Shift + V";

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

            if (!TryMapKeyToken(part, out var key, out var display))
            {
                return false;
            }

            virtualKey = key;
            displayParts.Add(display);
        }

        if (modifiers == 0 || virtualKey is null)
        {
            return false;
        }

        hotkey = new HotkeyDefinition(modifiers, virtualKey.Value, string.Join(" + ", displayParts));
        return true;
    }

    /// <summary>
    /// Build a hotkey from Win32-style modifier flags + virtual-key code (for capture UI).
    /// </summary>
    public static bool TryFromVirtualKey(uint modifiers, uint virtualKey, out HotkeyDefinition hotkey)
    {
        hotkey = default!;
        if (modifiers == 0 || virtualKey == 0)
        {
            return false;
        }

        // Reject pure modifiers as the main key.
        if (virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5)
        {
            return false;
        }

        if (!TryMapVirtualKey(virtualKey, out var display))
        {
            return false;
        }

        var parts = new List<string>();
        if ((modifiers & HotkeyModifier.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & HotkeyModifier.Alt) != 0) parts.Add("Alt");
        if ((modifiers & HotkeyModifier.Shift) != 0) parts.Add("Shift");
        if ((modifiers & HotkeyModifier.Win) != 0) parts.Add("Win");
        if (parts.Count == 0)
        {
            return false;
        }

        parts.Add(display);
        hotkey = new HotkeyDefinition(modifiers, virtualKey, string.Join(" + ", parts));
        return true;
    }

    private static bool TryMapKeyToken(string part, out uint virtualKey, out string display)
    {
        virtualKey = 0;
        display = part;

        if (part is "`" or "Backtick" or "Oem3" or "Grave")
        {
            virtualKey = HotkeyVirtualKey.Oem3;
            display = "`";
            return true;
        }

        if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
        {
            var key = char.ToUpperInvariant(part[0]);
            virtualKey = key;
            display = key.ToString();
            return true;
        }

        // Function keys F1-F12
        if (part.Length is >= 2 and <= 3 &&
            (part[0] is 'F' or 'f') &&
            int.TryParse(part[1..], out var fn) &&
            fn is >= 1 and <= 12)
        {
            virtualKey = (uint)(0x70 + fn - 1);
            display = $"F{fn}";
            return true;
        }

        return false;
    }

    private static bool TryMapVirtualKey(uint virtualKey, out string display)
    {
        if (virtualKey == HotkeyVirtualKey.Oem3)
        {
            display = "`";
            return true;
        }

        if (virtualKey is >= 0x30 and <= 0x39) // 0-9
        {
            display = ((char)virtualKey).ToString();
            return true;
        }

        if (virtualKey is >= 0x41 and <= 0x5A) // A-Z
        {
            display = ((char)virtualKey).ToString();
            return true;
        }

        if (virtualKey is >= 0x70 and <= 0x7B) // F1-F12
        {
            display = $"F{virtualKey - 0x70 + 1}";
            return true;
        }

        display = "";
        return false;
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
