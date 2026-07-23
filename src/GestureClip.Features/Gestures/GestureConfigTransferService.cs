using System.Text.Json;
using System.Text.Json.Serialization;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed class GestureConfigTransferService : IGestureConfigTransferService
{
    public const string FormatId = "GestureClip.GestureConfig";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ExportToJson(
        GesturePreset preset,
        IReadOnlyDictionary<string, BuiltInGestureAction> bindings,
        IReadOnlyDictionary<string, BuiltInGestureAction> leftButtonEnhanced)
    {
        var document = new GestureConfigDocument
        {
            Format = FormatId,
            Version = FormatVersion,
            ExportedAt = DateTimeOffset.Now.ToString("o"),
            Preset = preset,
            Bindings = bindings
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != BuiltInGestureAction.None)
                .ToDictionary(
                    pair => pair.Key.Trim().ToUpperInvariant(),
                    pair => new GestureConfigBindingDto
                    {
                        Action = pair.Value,
                        IsEnabled = true
                    },
                    StringComparer.Ordinal),
            LeftButtonEnhanced = leftButtonEnhanced
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != BuiltInGestureAction.None)
                .ToDictionary(
                    pair => pair.Key.Trim().ToUpperInvariant(),
                    pair => pair.Value,
                    StringComparer.Ordinal)
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public GestureConfigImportResult ImportFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("文件内容为空。");
        }

        GestureConfigDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<GestureConfigDocument>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            return Fail($"无法解析 JSON：{ex.Message}");
        }

        if (document is null)
        {
            return Fail("配置文件无效。");
        }

        if (!string.IsNullOrWhiteSpace(document.Format) &&
            !string.Equals(document.Format, FormatId, StringComparison.Ordinal))
        {
            return Fail($"不支持的配置格式：{document.Format}");
        }

        if (document.Version is > FormatVersion or < 1)
        {
            return Fail($"不支持的配置版本：{document.Version}");
        }

        var bindings = new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal);
        if (document.Bindings is not null)
        {
            foreach (var pair in document.Bindings)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                {
                    continue;
                }

                if (!pair.Value.IsEnabled || pair.Value.Action == BuiltInGestureAction.None)
                {
                    continue;
                }

                var pattern = NormalizePattern(pair.Key);
                if (pattern.Length == 0)
                {
                    continue;
                }

                bindings[pattern] = pair.Value.Action;
            }
        }

        // Compatibility: flat map of pattern -> action name/number
        if (bindings.Count == 0 && document.Actions is not null)
        {
            foreach (var pair in document.Actions)
            {
                var pattern = NormalizePattern(pair.Key);
                if (pattern.Length == 0 || pair.Value == BuiltInGestureAction.None)
                {
                    continue;
                }

                bindings[pattern] = pair.Value;
            }
        }

        if (bindings.Count == 0)
        {
            return Fail("配置里没有可用的手势绑定。");
        }

        var left = new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal);
        if (document.LeftButtonEnhanced is not null)
        {
            foreach (var pair in document.LeftButtonEnhanced)
            {
                var pattern = NormalizePattern(pair.Key);
                if (pattern.Length == 0 || pair.Value == BuiltInGestureAction.None)
                {
                    continue;
                }

                left[pattern] = pair.Value;
            }
        }

        if (left.Count == 0)
        {
            left = new Dictionary<string, BuiltInGestureAction>(
                GesturePresetProvider.DefaultLeftButtonEnhanced,
                StringComparer.Ordinal);
        }

        var preset = document.Preset == GesturePreset.ClipboardEnhanced ||
                     document.Preset == GesturePreset.EditEnhanced ||
                     document.Preset == GesturePreset.Custom
            ? document.Preset
            : GesturePreset.Custom;

        // Imported custom maps always land as Custom so they take effect.
        if (bindings.Count > 0)
        {
            preset = GesturePreset.Custom;
        }

        return new GestureConfigImportResult(
            true,
            $"已导入 {bindings.Count} 个手势绑定。",
            preset,
            bindings,
            left);
    }

    private static GestureConfigImportResult Fail(string message) =>
        new(false, message, GesturePreset.EditEnhanced,
            new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal),
            new Dictionary<string, BuiltInGestureAction>(GesturePresetProvider.DefaultLeftButtonEnhanced, StringComparer.Ordinal));

    private static string NormalizePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "";
        }

        var normalized = pattern.Trim().ToUpperInvariant();
        if (normalized is "R+L" or "右键+左键" or "右键按住+左键点击")
        {
            return "R+L";
        }

        return new string(normalized.Where(ch => ch is 'U' or 'D' or 'L' or 'R').ToArray());
    }

    private sealed class GestureConfigDocument
    {
        public string Format { get; set; } = FormatId;
        public int Version { get; set; } = FormatVersion;
        public string? ExportedAt { get; set; }
        public GesturePreset Preset { get; set; } = GesturePreset.Custom;
        public Dictionary<string, GestureConfigBindingDto>? Bindings { get; set; }
        public Dictionary<string, BuiltInGestureAction>? LeftButtonEnhanced { get; set; }
        /// <summary>Legacy / simplified map.</summary>
        public Dictionary<string, BuiltInGestureAction>? Actions { get; set; }
    }

    private sealed class GestureConfigBindingDto
    {
        public BuiltInGestureAction Action { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
