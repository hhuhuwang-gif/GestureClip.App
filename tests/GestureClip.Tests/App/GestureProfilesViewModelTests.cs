using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class GestureProfilesViewModelTests
{
    [Fact]
    public async Task Existing_profile_is_selected_when_editor_opens()
    {
        var profiles = new GestureAppProfileService(new FakeSettingsService());
        await profiles.SaveAsync([new("excel", true, [new("U", false, BuiltInGestureAction.Copy)])], default);

        var viewModel = new GestureProfilesViewModel(profiles);

        Assert.Equal("excel", viewModel.SelectedProfile?.ProcessName);
        Assert.True(viewModel.HasProfiles);
        Assert.True(viewModel.HasSelectedProfile);
        Assert.True(viewModel.HasBindings);
    }

    [Fact]
    public async Task Duplicate_creates_disabled_unsaved_draft_with_copied_bindings()
    {
        var profiles = new GestureAppProfileService(new FakeSettingsService());
        await profiles.SaveAsync([new("excel", true, [new("UD", false, BuiltInGestureAction.Copy)])], default);
        var viewModel = new GestureProfilesViewModel(profiles);

        viewModel.DuplicateSelectedProfile();

        Assert.Null(viewModel.SelectedProfile);
        Assert.Empty(viewModel.ProcessName);
        Assert.False(viewModel.IsEnabled);
        Assert.Equal("UD", Assert.Single(viewModel.Bindings).Pattern);
        Assert.Single(profiles.GetCurrent());
    }

    [Fact]
    public void Add_and_remove_binding_updates_empty_state()
    {
        var viewModel = new GestureProfilesViewModel(
            new GestureAppProfileService(new FakeSettingsService()));

        var row = viewModel.AddBinding();

        Assert.True(viewModel.HasBindings);
        viewModel.RemoveBinding(row);
        Assert.False(viewModel.HasBindings);
    }

    [Theory]
    [InlineData("Excel", "excel")]
    [InlineData("浏览器", "msedge")]
    [InlineData("编辑器", "code")]
    public void Templates_only_populate_disabled_unsaved_drafts(string template, string process)
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        var viewModel = new GestureProfilesViewModel(profiles);
        viewModel.ApplyTemplate(template);
        Assert.Equal(process, viewModel.ProcessName);
        Assert.False(viewModel.IsEnabled);
        Assert.Empty(profiles.GetCurrent());
        Assert.All(viewModel.Bindings, row => Assert.Contains(viewModel.Actions, option => option.Action == row.Action));
        Assert.Equal(0, settings.SetCount);
    }

    [Fact]
    public async Task Template_rejects_overwrite_of_existing_selected_profile()
    {
        var profiles = new GestureAppProfileService(new FakeSettingsService());
        await profiles.SaveAsync([new("excel", true, [new("U", false, BuiltInGestureAction.Redo)])], default);
        var viewModel = new GestureProfilesViewModel(profiles);
        viewModel.SelectedProfile = viewModel.Profiles[0];
        Assert.Throws<InvalidOperationException>(() => viewModel.ApplyTemplate("浏览器"));
        Assert.Equal(BuiltInGestureAction.Redo, Assert.Single(viewModel.Bindings).Action);
    }

    [Fact]
    public async Task Add_edit_disable_and_delete_profiles_persist_without_altering_global_settings()
    {
        var settings = new FakeSettingsService();
        var profiles = new GestureAppProfileService(settings);
        var viewModel = new GestureProfilesViewModel(profiles);
        viewModel.ApplyTemplate("Excel");
        viewModel.IsEnabled = true;
        await viewModel.SaveAsync();
        Assert.True(Assert.Single(profiles.GetCurrent()).IsEnabled);
        viewModel.Bindings[0].Action = BuiltInGestureAction.Redo;
        viewModel.IsEnabled = false;
        await viewModel.SaveAsync();
        Assert.False(Assert.Single(profiles.GetCurrent()).IsEnabled);
        Assert.Equal(BuiltInGestureAction.Redo, profiles.GetCurrent()[0].Bindings[0].Action);
        await viewModel.DeleteAsync();
        Assert.Empty(profiles.GetCurrent());
        Assert.Null(viewModel.SelectedProfile);
        Assert.Equal(GestureAppProfileService.SettingsKey, Assert.Single(settings.Values.Keys));
    }
}
