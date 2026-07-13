using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using System.Text.Json;

namespace GestureClip.Features.Gestures;

public sealed class GesturePresetProvider : IGesturePresetProvider
{
    private static readonly IReadOnlyDictionary<string, BuiltInGestureAction> EditEnhanced =
        new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["U"] = BuiltInGestureAction.Copy,
            ["D"] = BuiltInGestureAction.SmartPaste,
            ["UD"] = BuiltInGestureAction.Enter,
            ["DU"] = BuiltInGestureAction.Escape,
            ["L"] = BuiltInGestureAction.SendAltLeft,
            ["R"] = BuiltInGestureAction.SendAltRight,
            ["LR"] = BuiltInGestureAction.SelectAll,
            ["RL"] = BuiltInGestureAction.Undo,
            ["DL"] = BuiltInGestureAction.PasteAndEnter,
            ["R+L"] = BuiltInGestureAction.PasteAndEnter,
            ["DR"] = BuiltInGestureAction.NewTab,
            ["UR"] = BuiltInGestureAction.SearchSelectedTextWithGoogle,
            ["UL"] = BuiltInGestureAction.SearchSelectedTextWithBaidu
        };

    private static readonly IReadOnlyDictionary<string, BuiltInGestureAction> ClipboardEnhanced =
        new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["U"] = BuiltInGestureAction.OpenClipboardOverlay,
            ["D"] = BuiltInGestureAction.PasteLatestClipboardItem,
            ["UD"] = BuiltInGestureAction.Enter,
            ["DU"] = BuiltInGestureAction.Escape,
            ["L"] = BuiltInGestureAction.SendAltLeft,
            ["R"] = BuiltInGestureAction.SendAltRight,
            ["LR"] = BuiltInGestureAction.SelectAll,
            ["RL"] = BuiltInGestureAction.Undo,
            ["DL"] = BuiltInGestureAction.PasteAndEnter,
            ["R+L"] = BuiltInGestureAction.PasteAndEnter,
            ["DR"] = BuiltInGestureAction.NewTab,
            ["UR"] = BuiltInGestureAction.SearchSelectedTextWithGoogle,
            ["UL"] = BuiltInGestureAction.SearchSelectedTextWithBaidu
        };

    public static readonly IReadOnlyDictionary<string, BuiltInGestureAction> DefaultLeftButtonEnhanced =
        new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["D"] = BuiltInGestureAction.SmartPaste,
            ["U"] = BuiltInGestureAction.SelectAll
        };

    private readonly object _syncRoot = new();
    private IReadOnlyDictionary<string, BuiltInGestureAction> _customBindings = EditEnhanced;
    private IReadOnlyDictionary<string, BuiltInGestureAction> _leftButtonEnhanced = DefaultLeftButtonEnhanced;

    public GesturePresetProvider()
    {
    }

    public GesturePresetProvider(ISettingsService settingsService)
    {
        _customBindings = ParseCustomBindings(settingsService.Get(SettingKeys.GestureCustomBindingsJson, ""));
        _leftButtonEnhanced = ParseLeftButtonEnhanced(settingsService.Get(SettingKeys.GestureLeftButtonEnhancedJson, ""));
    }

    public BuiltInGestureAction GetAction(GesturePreset preset, string pattern)
    {
        var map = GetBindings(preset);
        return map.TryGetValue(pattern, out var action) ? action : BuiltInGestureAction.None;
    }

    public BuiltInGestureAction GetAction(GesturePreset preset, GestureExecutionContext context)
    {
        if (context.IsLeftButtonModified)
        {
            var leftMap = GetLeftButtonEnhancedBindings();
            if (leftMap.TryGetValue(context.Pattern, out var enhancedAction) &&
                enhancedAction != BuiltInGestureAction.None)
            {
                return enhancedAction;
            }
        }

        return GetAction(preset, context.Pattern);
    }

    public IReadOnlyDictionary<string, BuiltInGestureAction> GetBindings(GesturePreset preset)
    {
        if (preset == GesturePreset.Custom)
        {
            lock (_syncRoot)
            {
                return _customBindings;
            }
        }

        return preset == GesturePreset.ClipboardEnhanced ? ClipboardEnhanced : EditEnhanced;
    }

    public void UpdateCustomBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
    {
        lock (_syncRoot)
        {
            _customBindings = new Dictionary<string, BuiltInGestureAction>(bindings, StringComparer.Ordinal);
        }
    }

    public IReadOnlyDictionary<string, BuiltInGestureAction> GetLeftButtonEnhancedBindings()
    {
        lock (_syncRoot)
        {
            return _leftButtonEnhanced;
        }
    }

    public void UpdateLeftButtonEnhancedBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
    {
        lock (_syncRoot)
        {
            _leftButtonEnhanced = new Dictionary<string, BuiltInGestureAction>(bindings, StringComparer.Ordinal);
        }
    }

    private static IReadOnlyDictionary<string, BuiltInGestureAction> ParseCustomBindings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EditEnhanced;
        }

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, CustomBindingDto>>(json);
            if (map is null || map.Count == 0)
            {
                return EditEnhanced;
            }

            return map
                .Where(pair => pair.Value.IsEnabled)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Action, StringComparer.Ordinal);
        }
        catch
        {
            return EditEnhanced;
        }
    }

    private static IReadOnlyDictionary<string, BuiltInGestureAction> ParseLeftButtonEnhanced(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DefaultLeftButtonEnhanced;
        }

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, BuiltInGestureAction>>(json);
            if (map is null || map.Count == 0)
            {
                return DefaultLeftButtonEnhanced;
            }

            return map
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != BuiltInGestureAction.None)
                .ToDictionary(pair => pair.Key.Trim().ToUpperInvariant(), pair => pair.Value, StringComparer.Ordinal);
        }
        catch
        {
            return DefaultLeftButtonEnhanced;
        }
    }

    private sealed class CustomBindingDto
    {
        public BuiltInGestureAction Action { get; set; }
        public string? Shortcut { get; set; }
        public bool IsEnabled { get; set; }
    }
}
