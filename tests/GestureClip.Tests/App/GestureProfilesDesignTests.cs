using Xunit;

namespace GestureClip.Tests.App;

public sealed class GestureProfilesDesignTests
{
    [Fact]
    public void Window_exposes_premium_layout_empty_states_and_shortcuts()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "GestureProfilesWindow.xaml"));
        var code = File.ReadAllText(Find("src", "GestureClip.App", "GestureProfilesWindow.xaml.cs"));

        Assert.Contains("ProfileAmbient", xaml);
        Assert.Contains("BrushOverlayPanel", xaml);
        Assert.Contains("HasProfiles", xaml);
        Assert.Contains("HasBindings", xaml);
        Assert.Contains("ProfileFocusVisual", xaml);
        Assert.Contains("AutomationProperties.Name", xaml);
        Assert.Contains("Ctrl+N 新建", xaml);
        Assert.Contains("AppendDirection_Click", xaml);
        Assert.Contains("HandleControlShortcutAsync", code);
        Assert.Contains("Key.Insert", code);
        Assert.Contains("Key.Delete", code);
    }

    [Fact]
    public void Reminder_uses_theme_surfaces_and_clear_action_hierarchy()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "OverworkReminderToastWindow.xaml"));

        Assert.Contains("ReminderAmbient", xaml);
        Assert.Contains("BrushOverlayPanel", xaml);
        Assert.Contains("BrushTextPrimary", xaml);
        Assert.Contains("PrimaryButtonStyle", xaml);
        Assert.Contains("SecondaryButtonStyle", xaml);
        Assert.Contains("AutomationProperties.Name", xaml);
        Assert.DoesNotContain("#F4FFFFFF", xaml);
        Assert.DoesNotContain("Foreground=\"#111827\"", xaml);
    }

    private static string Find(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
