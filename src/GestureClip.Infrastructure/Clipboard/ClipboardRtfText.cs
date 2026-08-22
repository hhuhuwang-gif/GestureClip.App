using System.Text;

namespace GestureClip.Infrastructure.Clipboard;

/// <summary>Small best-effort RTF-to-text reader for clipboard payloads.</summary>
public static class ClipboardRtfText
{
    private static readonly HashSet<string> Destinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "header", "footer",
        "headerl", "headerr", "footerl", "footerr", "generator", "themedata", "datastore",
        "xmlnstbl", "listtable", "listoverridetable", "rsidtbl", "latentstyles"
    };

    public static string? ExtractPlain(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return null;
        }

        try
        {
            var output = new StringBuilder(rtf.Length);
            var stack = new Stack<State>();
            var state = new State();
            var unicodeFallbackToSkip = 0;

            for (var index = 0; index < rtf.Length; index++)
            {
                var character = rtf[index];
                if (character == '{')
                {
                    stack.Push(state.Clone());
                    continue;
                }

                if (character == '}')
                {
                    state = stack.Count > 0 ? stack.Pop() : new State();
                    unicodeFallbackToSkip = 0;
                    continue;
                }

                if (character != '\\')
                {
                    if (unicodeFallbackToSkip > 0)
                    {
                        if (character is not '\r' and not '\n')
                        {
                            unicodeFallbackToSkip--;
                        }

                        continue;
                    }

                    if (!state.Skip && character is not '\r' and not '\n')
                    {
                        output.Append(character);
                    }

                    continue;
                }

                if (++index >= rtf.Length)
                {
                    break;
                }

                var escaped = rtf[index];
                if (escaped is '\\' or '{' or '}')
                {
                    if (unicodeFallbackToSkip > 0)
                    {
                        unicodeFallbackToSkip--;
                    }
                    else if (!state.Skip)
                    {
                        output.Append(escaped);
                    }

                    continue;
                }

                if (escaped == '*')
                {
                    state.Skip = true;
                    continue;
                }

                if (escaped == '\'')
                {
                    if (index + 2 < rtf.Length &&
                        byte.TryParse(rtf.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var value))
                    {
                        index += 2;
                        if (unicodeFallbackToSkip > 0)
                        {
                            unicodeFallbackToSkip--;
                        }
                        else if (!state.Skip)
                        {
                            output.Append((char)value);
                        }
                    }

                    continue;
                }

                if (!char.IsLetter(escaped))
                {
                    if (!state.Skip)
                    {
                        output.Append(escaped switch
                        {
                            '~' => ' ',
                            '_' => '-',
                            _ => '\0'
                        });
                        if (output.Length > 0 && output[^1] == '\0')
                        {
                            output.Length--;
                        }
                    }

                    continue;
                }

                var wordStart = index;
                while (index + 1 < rtf.Length && char.IsLetter(rtf[index + 1]))
                {
                    index++;
                }

                var word = rtf[wordStart..(index + 1)];
                var sign = 1;
                if (index + 1 < rtf.Length && rtf[index + 1] == '-')
                {
                    sign = -1;
                    index++;
                }

                var numberStart = index + 1;
                while (index + 1 < rtf.Length && char.IsDigit(rtf[index + 1]))
                {
                    index++;
                }

                int? parameter = null;
                if (index >= numberStart)
                {
                    parameter = sign * int.Parse(rtf[numberStart..(index + 1)], System.Globalization.CultureInfo.InvariantCulture);
                }

                if (index + 1 < rtf.Length && rtf[index + 1] == ' ')
                {
                    index++;
                }

                if (Destinations.Contains(word))
                {
                    state.Skip = true;
                    continue;
                }

                if (word.Equals("uc", StringComparison.OrdinalIgnoreCase) && parameter is { } uc)
                {
                    state.UnicodeFallbackCount = Math.Max(0, uc);
                    continue;
                }

                if (state.Skip)
                {
                    continue;
                }

                if (word.Equals("u", StringComparison.OrdinalIgnoreCase) && parameter is { } unicode)
                {
                    output.Append((char)(unicode < 0 ? unicode + 65536 : unicode));
                    unicodeFallbackToSkip = state.UnicodeFallbackCount;
                    continue;
                }

                if (word.Equals("bin", StringComparison.OrdinalIgnoreCase) && parameter is > 0)
                {
                    index = Math.Min(rtf.Length - 1, index + parameter.Value);
                    continue;
                }

                switch (word.ToLowerInvariant())
                {
                    case "par":
                    case "line":
                        output.Append('\n');
                        break;
                    case "tab":
                        output.Append('\t');
                        break;
                    case "emdash":
                        output.Append('—');
                        break;
                    case "endash":
                        output.Append('–');
                        break;
                    case "bullet":
                        output.Append('•');
                        break;
                    case "lquote":
                    case "rquote":
                        output.Append('\'');
                        break;
                    case "ldblquote":
                    case "rdblquote":
                        output.Append('"');
                        break;
                }
            }

            var plain = output.ToString()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();
            return string.IsNullOrWhiteSpace(plain) ? null : plain;
        }
        catch
        {
            return null;
        }
    }

    private sealed class State
    {
        public bool Skip { get; set; }
        public int UnicodeFallbackCount { get; set; } = 1;
        public State Clone() => new() { Skip = Skip, UnicodeFallbackCount = UnicodeFallbackCount };
    }
}
