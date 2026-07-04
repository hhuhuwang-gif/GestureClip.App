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
            ["D"] = BuiltInGestureAction.Paste,
            ["UD"] = BuiltInGestureAction.Enter,
            ["DU"] = BuiltInGestureAction.Escape,
            ["L"] = BuiltInGestureAction.SendAltLeft,
            ["R"] = BuiltInGestureAction.SendAltRight,
            ["LR"] = BuiltInGestureAction.SelectAll,
            ["RL"] = BuiltInGestureAction.Undo,
            ["DL"] = BuiltInGestureAction.LeftMouseClick,
            ["DR"] = BuiltInGestureAction.RightMouseClick,
            ["UR"] = BuiltInGestureAction.NewTab,
            ["UL"] = BuiltInGestureAction.ReopenClosedTab,
            ["RU"] = BuiltInGestureAction.Refresh,
            ["RD"] = BuiltInGestureAction.CloseTab,
            ["LD"] = BuiltInGestureAction.MinimizeForegroundWindow,
            ["RDL"] = BuiltInGestureAction.Screenshot,
            ["RUD"] = BuiltInGestureAction.ResetZoom,
            ["URD"] = BuiltInGestureAction.NextTab,
            ["ULD"] = BuiltInGestureAction.PreviousTab,
            ["RULD"] = BuiltInGestureAction.SystemSettings
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
            ["DL"] = BuiltInGestureAction.LeftMouseClick,
            ["DR"] = BuiltInGestureAction.RightMouseClick,
            ["UR"] = BuiltInGestureAction.NewTab,
            ["UL"] = BuiltInGestureAction.ReopenClosedTab,
            ["RU"] = BuiltInGestureAction.Refresh,
            ["RD"] = BuiltInGestureAction.CloseTab,
            ["LD"] = BuiltInGestureAction.MinimizeForegroundWindow,
            ["RDL"] = BuiltInGestureAction.Screenshot,
            ["RUD"] = BuiltInGestureAction.ResetZoom,
            ["URD"] = BuiltInGestureAction.NextTab,
            ["ULD"] = BuiltInGestureAction.PreviousTab,
            ["RULD"] = BuiltInGestureAction.SystemSettings
        };

    private readonly object _syncRoot = new();
    private IReadOnlyDictionary<string, BuiltInGestureAction> _customBindings = EditEnhanced;

    public GesturePresetProvider()
    {
    }

    public GesturePresetProvider(ISettingsService settingsService)
    {
        _customBindings = ParseCustomBindings(settingsService.Get(SettingKeys.GestureCustomBindingsJson, ""));
    }

    public BuiltInGestureAction GetAction(GesturePreset preset, string pattern)
    {
        var map = GetBindings(preset);

        return map.TryGetValue(pattern, out var action) ? action : BuiltInGestureAction.None;
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

    private sealed class CustomBindingDto
    {
        public BuiltInGestureAction Action { get; set; }
        public string? Shortcut { get; set; }
        public bool IsEnabled { get; set; }
    }
}
