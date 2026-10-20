using System.Text.Json;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureAppProfileServiceTests
{
    [Fact]
    public void Missing_configuration_preserves_global_and_modifier_bindings()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        var provider = new GesturePresetProvider(settings, profiles);
        Assert.Empty(profiles.GetCurrent());
        Assert.Equal(BuiltInGestureAction.Copy,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("U", false, TargetProcessName: "excel.exe")));
        Assert.Equal(BuiltInGestureAction.SelectAll,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("U", true, TargetProcessName: "excel.exe")));
    }

    [Fact]
    public async Task Enabled_profile_matches_exact_normalized_process_and_binding_mode()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        await profiles.SaveAsync([Profile(" EXCEL.EXE ", new GestureAppBinding("U", false, BuiltInGestureAction.Redo))], default);
        var provider = new GesturePresetProvider(settings, profiles);
        Assert.Equal(BuiltInGestureAction.Redo,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("U", false, TargetProcessName: "excel")));
        Assert.Equal(BuiltInGestureAction.Copy,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("U", false, TargetProcessName: "excelhelper.exe")));
        Assert.Equal(BuiltInGestureAction.SelectAll,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("U", true, TargetProcessName: "excel.exe")));
        Assert.Equal(BuiltInGestureAction.Undo,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("RL", false, TargetProcessName: "excel.exe")));
    }

    [Fact]
    public async Task Explicit_none_disables_action_including_wheel_without_inheriting_global()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        await profiles.SaveAsync([Profile("code", new GestureAppBinding("U", false, BuiltInGestureAction.None),
            new GestureAppBinding("WHEEL_UP", false, BuiltInGestureAction.None))], default);
        var provider = new GesturePresetProvider(settings, profiles);
        var context = new GestureExecutionContext("U", false, TargetProcessName: "code.exe");
        Assert.Equal(BuiltInGestureAction.None, provider.GetAction(GesturePreset.EditEnhanced, context));
        Assert.Equal(BuiltInGestureAction.None,
            provider.GetWheelAction(context with { Pattern = "WHEEL_UP" }, BuiltInGestureAction.PreviousTab));
        Assert.Equal(BuiltInGestureAction.NextTab,
            provider.GetWheelAction(context with { Pattern = "WHEEL_DOWN" }, BuiltInGestureAction.NextTab));
    }

    [Fact]
    public async Task Disabled_profile_and_unknown_target_inherit_global()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        await profiles.SaveAsync([Profile("excel", new GestureAppBinding("U", false, BuiltInGestureAction.Redo)) with { IsEnabled = false }], default);
        Assert.False(profiles.TryGetAction(new("U", false, TargetProcessName: "excel.exe"), out _));
        Assert.False(profiles.TryGetAction(new("U", false), out _));
    }

    [Fact]
    public async Task Save_roundtrips_and_snapshots_input_bindings()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        var bindings = new List<GestureAppBinding> { new("D", false, BuiltInGestureAction.PastePlainText) };
        await profiles.SaveAsync([new("chrome.exe", true, bindings)], default);
        bindings.Clear();
        var reloaded = new GestureAppProfileService(settings);
        Assert.Single(profiles.GetCurrent()[0].Bindings);
        Assert.True(reloaded.TryGetAction(new("D", false, TargetProcessName: "CHROME.EXE"), out var action));
        Assert.Equal(BuiltInGestureAction.PastePlainText, action);
        await profiles.SaveAsync([], default);
        Assert.False(profiles.TryGetAction(new("D", false, TargetProcessName: "chrome.exe"), out _));
    }

    [Theory]
    [InlineData("chrome*")]
    [InlineData("C:\\apps\\excel.exe")]
    [InlineData("../code.exe")]
    [InlineData(".exe")]
    public async Task Invalid_process_is_rejected_without_persisting(string process)
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        await Assert.ThrowsAsync<ArgumentException>(() => profiles.SaveAsync([Profile(process)], default));
        Assert.Equal(0, settings.SetCount);
    }

    [Fact]
    public async Task Duplicate_process_or_duplicate_mode_pattern_is_rejected()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        await Assert.ThrowsAsync<ArgumentException>(() => profiles.SaveAsync([Profile("EXCEL.exe"), Profile("excel")], default));
        var duplicate = Profile("excel", new GestureAppBinding("D", false, BuiltInGestureAction.Copy), new GestureAppBinding("D", false, BuiltInGestureAction.Paste));
        await Assert.ThrowsAsync<ArgumentException>(() => profiles.SaveAsync([duplicate], default));
        Assert.Equal(0, settings.SetCount);
    }

    [Fact]
    public void Corrupt_json_surfaces_configuration_error()
    {
        var settings = new FakeSettingsService();
        settings.Values[GestureAppProfileService.SettingsKey] = "{broken";
        Assert.Throws<JsonException>(() => new GestureAppProfileService(settings));
    }

    private static GestureAppProfile Profile(string process, params GestureAppBinding[] bindings) => new(process, true, bindings);
}
