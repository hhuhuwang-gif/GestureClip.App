using System.Xml.Linq;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class GestureOverlayWindowTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void GestureOverlayWindow_uses_dark_surface_with_narrow_time_theme_rail()
    {
        var documents = LoadHudDocuments();
        var panel = FindNamedElement(documents[0], "CompactGameHud");
        Assert.Equal("#F0111724", (string?)panel.Attribute("Background"));
        var rail = FindNamedElement(documents[0], "HudThemeRail");
        Assert.Equal("{Binding HudBackgroundBrush}", (string?)rail.Attribute("Background"));
        Assert.Contains(panel, rail.Ancestors());
        var columns = rail.Parent!.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions");
        Assert.Equal("4", (string?)columns.Elements().First().Attribute("Width"));
        Assert.DoesNotContain(documents.SelectMany(document => document.Descendants()),
            element => element.Name.LocalName == "DropShadowEffect");
        Assert.DoesNotContain(documents.SelectMany(document => document.Descendants()).SelectMany(element => element.Attributes()),
            attribute => attribute.Value.Any(character => character is >= '\uE000' and <= '\uF8FF'));
    }

    [Fact]
    public void GestureOverlayWindow_keeps_bound_unbound_and_pending_hints_in_used_style()
    {
        var documents = LoadHudDocuments();
        var hint = documents[0].Descendants().Single(element =>
            (string?)element.Attribute("Style") == "{StaticResource HudReleaseHint}");
        Assert.Equal("TextBlock", hint.Name.LocalName);
        var style = FindStyle(documents, "HudReleaseHint");
        Assert.Contains(style.Elements(), element =>
            (string?)element.Attribute("Property") == "Text" &&
            (string?)element.Attribute("Value") == "松开右键执行");
        var triggers = style.Descendants().Where(element => element.Name.LocalName == "DataTrigger").ToArray();
        AssertHintTrigger(triggers, ("{Binding ActionName}", "未绑定", "未绑定动作"));
        AssertHintTrigger(triggers, ("{Binding Pattern}", "-", "划动以识别手势"));
        Assert.Equal("{Binding Pattern}", (string?)triggers.Last().Attribute("Binding"));
    }

    [Fact]
    public void GestureOverlayWindow_preserves_actual_trace_and_status_bindings_without_interactions()
    {
        var window = LoadHudDocuments()[0];
        var textBindings = window.Descendants().Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => (string?)element.Attribute("Text")).ToArray();
        foreach (var property in new[]
        {
            "DirectionText", "ActionName", "ShortcutText", "PresetName", "Pattern", "WorkStatusText",
            "GainedXpText", "FunText", "LevelText", "XpText", "WorkSummaryText", "GestureCountText",
            "CopyCountText", "PasteCountText", "SavedClicksCountText"
        }) Assert.Contains("{Binding " + property + "}", textBindings);
        foreach (var name in new[] { "TrailLine", "TrailGlowLine", "TrailFeedbackLine" })
        {
            var trace = FindNamedElement(window, name);
            Assert.Equal("Polyline", trace.Name.LocalName);
            Assert.Equal("{Binding Points}", (string?)trace.Attribute("Points"));
        }
        Assert.Equal("{Binding StrokeBrush}", (string?)FindNamedElement(window, "TrailLine").Attribute("Stroke"));
        Assert.Equal("{Binding StrokeBrush}", (string?)FindNamedElement(window, "TrailGlowLine").Attribute("Stroke"));
        Assert.DoesNotContain(window.Descendants(), element => element.Name.LocalName == "Button" || element.Attribute("Command") is not null);
        AssertXpProgressStyleRange();
    }

    private static void AssertHintTrigger(IReadOnlyList<XElement> triggers,
        (string Binding, string Value, string Text) expected)
    {
        var trigger = triggers.Single(element => (string?)element.Attribute("Binding") == expected.Binding &&
            (string?)element.Attribute("Value") == expected.Value);
        Assert.Contains(trigger.Elements(), element => (string?)element.Attribute("Property") == "Text" &&
            (string?)element.Attribute("Value") == expected.Text);
    }

    private static void AssertXpProgressStyleRange()
    {
        var documents = LoadHudDocuments();
        var progress = documents[0].Descendants().Single(element => element.Name.LocalName == "ProgressBar");
        Assert.Equal("{Binding XpProgressPercent}", (string?)progress.Attribute("Value"));
        Assert.Equal("{Binding HudAccentBrush}", (string?)progress.Attribute("Foreground"));
        Assert.Equal("{StaticResource HudXpProgress}", (string?)progress.Attribute("Style"));
        var style = FindStyle(documents, "HudXpProgress");
        Assert.Contains(style.Elements(), element => (string?)element.Attribute("Property") == "Minimum" && (string?)element.Attribute("Value") == "0");
        Assert.Contains(style.Elements(), element => (string?)element.Attribute("Property") == "Maximum" && (string?)element.Attribute("Value") == "1");
        Assert.NotNull(FindNamedElement(style, "PART_Track"));
        Assert.NotNull(FindNamedElement(style, "PART_Indicator"));
    }

    private static void AssertNormalRightClickNeedsMovement()
    {
        var movement = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Features", "Gestures", "MouseGestureService.Move.cs"));
        Assert.Contains("distanceSquaredFromStart >= Square(_activeSettings.Options.TriggerThreshold)", movement);
        Assert.Contains("_gestureOverlayService.ShowGestureStartAsync", movement);
    }

    private static XElement FindNamedElement(XContainer document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(XamlNamespace + "Name") == name);

    private static XElement FindStyle(IReadOnlyList<XDocument> documents, string key) =>
        documents.SelectMany(document => document.Descendants()).Single(element =>
            element.Name.LocalName == "Style" && (string?)element.Attribute(XamlNamespace + "Key") == key);

    private static IReadOnlyList<XDocument> LoadHudDocuments() => RepositoryXaml.LoadDocuments(
        FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml"));

    private static string ReadHudXaml() => string.Join(Environment.NewLine, LoadHudDocuments());
}
