using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureConfigTransferServiceTests
{
    private readonly GestureConfigTransferService _sut = new();

    [Fact]
    public void Export_and_import_roundtrip_preserves_bindings()
    {
        var bindings = new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["U"] = BuiltInGestureAction.Copy,
            ["D"] = BuiltInGestureAction.PastePlainText,
            ["LR"] = BuiltInGestureAction.SelectAll
        };
        var left = new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["D"] = BuiltInGestureAction.SmartPaste
        };

        var json = _sut.ExportToJson(GesturePreset.Custom, bindings, left);
        Assert.Contains("GestureClip.GestureConfig", json);
        Assert.Contains("PastePlainText", json);

        var result = _sut.ImportFromJson(json);
        Assert.True(result.Success);
        Assert.Equal(3, result.Bindings.Count);
        Assert.Equal(BuiltInGestureAction.PastePlainText, result.Bindings["D"]);
        Assert.Equal(BuiltInGestureAction.SmartPaste, result.LeftButtonEnhanced["D"]);
        Assert.Equal(GesturePreset.Custom, result.Preset);
    }

    [Fact]
    public void Import_accepts_simplified_actions_map()
    {
        var json = """
        {
          "format": "GestureClip.GestureConfig",
          "version": 1,
          "actions": {
            "U": "OpenClipboardOverlay",
            "D": "SmartPaste"
          }
        }
        """;

        var result = _sut.ImportFromJson(json);
        Assert.True(result.Success);
        Assert.Equal(BuiltInGestureAction.OpenClipboardOverlay, result.Bindings["U"]);
        Assert.Equal(BuiltInGestureAction.SmartPaste, result.Bindings["D"]);
    }

    [Fact]
    public void Import_rejects_empty_or_invalid()
    {
        Assert.False(_sut.ImportFromJson("").Success);
        Assert.False(_sut.ImportFromJson("{ not json").Success);
        Assert.False(_sut.ImportFromJson("""{"format":"GestureClip.GestureConfig","version":1,"bindings":{}}""").Success);
    }
}
