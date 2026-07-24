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
    /// Build a hotkey from Win32 modifier flags + virtual-key code (capture UI).
    /// Requires at least one modifier (Ctrl/Alt/Shift/Win).
    /// </summary>
    public static bool TryFromVirtualKey(uint modifiers, uint virtualKey, out HotkeyDefinition hotkey)
    {
        hotkey = default!;
        if (modifiers == 0 || virtualKey == 0)
        {
            return false;
        }

        // Reject pure modifiers as the main key.
        if (IsModifierVirtualKey(virtualKey))
        {
            return false;
        }

        if (!TryMapVirtualKey(virtualKey, out var display))
        {
            return false;
        }

        var parts = new List<string>();
        if ((modifiers & HotkeyModifier.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & HotkeyModifier.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & HotkeyModifier.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & HotkeyModifier.Win) != 0)
        {
            parts.Add("Win");
        }

        if (parts.Count == 0)
        {
            return false;
        }

        parts.Add(display);
        hotkey = new HotkeyDefinition(modifiers, virtualKey, string.Join(" + ", parts));
        return true;
    }

    public static bool IsModifierVirtualKey(uint virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C
            or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    private static bool TryMapKeyToken(string part, out uint virtualKey, out string display)
    {
        virtualKey = 0;
        display = part;
        var token = part.Trim();

        // Normalize common aliases
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["`"] = "Oem3",
            ["Backtick"] = "Oem3",
            ["Grave"] = "Oem3",
            ["Space"] = "Space",
            ["Spacebar"] = "Space",
            ["Esc"] = "Escape",
            ["Escape"] = "Escape",
            ["Del"] = "Delete",
            ["Ins"] = "Insert",
            ["PgUp"] = "PageUp",
            ["PgDn"] = "PageDown",
            ["↑"] = "Up",
            ["↓"] = "Down",
            ["←"] = "Left",
            ["→"] = "Right",
        };
        if (aliases.TryGetValue(token, out var mapped))
        {
            token = mapped;
        }

        if (token.Equals("Oem3", StringComparison.OrdinalIgnoreCase))
        {
            virtualKey = HotkeyVirtualKey.Oem3;
            display = "`";
            return true;
        }

        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            var key = char.ToUpperInvariant(token[0]);
            virtualKey = key;
            display = key.ToString();
            return true;
        }

        // Function keys F1-F24
        if (token.Length is >= 2 and <= 3 &&
            (token[0] is 'F' or 'f') &&
            int.TryParse(token[1..], out var fn) &&
            fn is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + fn - 1);
            display = $"F{fn}";
            return true;
        }


        // Numpad 0-9 as Num0..Num9
        if (token.StartsWith("Num", StringComparison.OrdinalIgnoreCase) &&
            token.Length == 4 &&
            char.IsDigit(token[3]))
        {
            var digit = token[3] - '0';
            virtualKey = (uint)(0x60 + digit);
            display = $"Num{digit}";
            return true;
        }

        // Named keys
        var named = new Dictionary<string, (uint Vk, string Display)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = (0x20, "Space"),
            ["Tab"] = (0x09, "Tab"),
            ["Enter"] = (0x0D, "Enter"),
            ["Return"] = (0x0D, "Enter"),
            ["Escape"] = (0x1B, "Esc"),
            ["Backspace"] = (0x08, "Backspace"),
            ["Delete"] = (0x2E, "Delete"),
            ["Insert"] = (0x2D, "Insert"),
            ["Home"] = (0x24, "Home"),
            ["End"] = (0x23, "End"),
            ["PageUp"] = (0x21, "PageUp"),
            ["PageDown"] = (0x22, "PageDown"),
            ["Up"] = (0x26, "Up"),
            ["Down"] = (0x28, "Down"),
            ["Left"] = (0x25, "Left"),
            ["Right"] = (0x27, "Right"),
            ["OemMinus"] = (0xBD, "-"),
            ["OemPlus"] = (0xBB, "="),
            ["OemComma"] = (0xBC, ","),
            ["OemPeriod"] = (0xBE, "."),
            ["Oem2"] = (0xBF, "/"),
            ["Oem1"] = (0xBA, ";"),
            ["Oem7"] = (0xDE, "'"),
            ["Oem4"] = (0xDB, "["),
            ["Oem6"] = (0xDD, "]"),
            ["Oem5"] = (0xDC, "\\"),
            ["-"] = (0xBD, "-"),
            ["="] = (0xBB, "="),
            [","] = (0xBC, ","),
            ["."] = (0xBE, "."),
            ["/"] = (0xBF, "/"),
            [";"] = (0xBA, ";"),
            ["'"] = (0xDE, "'"),
            ["["] = (0xDB, "["),
            ["]"] = (0xDD, "]"),
            ["\\"] = (0xDC, "\\"),
        };

        if (named.TryGetValue(token, out var entry))
        {
            virtualKey = entry.Vk;
            display = entry.Display;
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

        if (virtualKey is >= 0x70 and <= 0x87) // F1-F24
        {
            display = $"F{virtualKey - 0x70 + 1}";
            return true;
        }

        // Numpad 0-9
        if (virtualKey is >= 0x60 and <= 0x69)
        {
            display = $"Num{virtualKey - 0x60}";
            return true;
        }

        display = virtualKey switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x2D => "Insert",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x26 => "Up",
            0x28 => "Down",
            0x25 => "Left",
            0x27 => "Right",
            0xBD => "-",
            0xBB => "=",
            0xBC => ",",
            0xBE => ".",
            0xBF => "/",
            0xBA => ";",
            0xDE => "'",
            0xDB => "[",
            0xDD => "]",
            0xDC => "\\",
            0xC0 => "`",
            // Some layouts report tilde as 0xC0 already; VK_OEM_8 rare
            _ => ""
        };

        return display.Length > 0;
    }

    public static IReadOnlyList<HotkeyDefinition> SuggestAlternatives(HotkeyDefinition current, int count = 6)
    {
        var suggestions = new List<HotkeyDefinition>();
        var baseMods = new[]
        {
            HotkeyModifier.Control | HotkeyModifier.Alt,
            HotkeyModifier.Control | HotkeyModifier.Shift,
            HotkeyModifier.Alt | HotkeyModifier.Shift,
            HotkeyModifier.Control | HotkeyModifier.Alt | HotkeyModifier.Shift,
            HotkeyModifier.Win | HotkeyModifier.Shift,
            HotkeyModifier.Control | HotkeyModifier.Win,
        };

        var keys = new uint[]
        {
            current.VirtualKey,
            (uint)'V', (uint)'C', (uint)'H', (uint)'J', (uint)'K', (uint)'L',
            (uint)'Q', (uint)'B', (uint)'N', HotkeyVirtualKey.Oem3, 0x20
        };

        foreach (var mods in baseMods)
        {
            foreach (var key in keys.Distinct())
            {
                if (!TryFromVirtualKey(mods, key, out var candidate))
                {
                    continue;
                }

                if (string.Equals(candidate.DisplayText, current.DisplayText, StringComparison.Ordinal))
                {
                    continue;
                }

                if (suggestions.Any(s => string.Equals(s.DisplayText, candidate.DisplayText, StringComparison.Ordinal)))
                {
                    continue;
                }

                suggestions.Add(candidate);
                if (suggestions.Count >= count)
                {
                    return suggestions;
                }
            }
        }

        return suggestions;
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
