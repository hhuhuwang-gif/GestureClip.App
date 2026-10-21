using Xunit;

namespace GestureClip.Tests.App;

public sealed class QuickActionCenterDesignTests
{
    [Fact]
    public void Window_exposes_premium_command_layout_and_complete_states()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "QuickActionCenterWindow.xaml"));
        var code = File.ReadAllText(Find("src", "GestureClip.App", "QuickActionCenterWindow.xaml.cs"));

        Assert.Contains("CommandAmbient", xaml);
        Assert.Contains("BrushOverlayPanel", xaml);
        Assert.Contains("FilteredActions.Count", xaml);
        Assert.Contains("HasFilteredActions", xaml);
        Assert.Contains("PreviewSelectedCommand", xaml);
        Assert.Contains("IsBusy", xaml);
        Assert.Contains("InputHint", xaml);
        Assert.Contains("PreviewText, Mode=OneWay", xaml);
        Assert.Contains("Shift+Enter 预览", xaml);
        Assert.Contains("AutomationProperties.Name", xaml);
        Assert.Contains("ScrollIntoView", code);
        Assert.Contains("Key.F5", code);
        Assert.DoesNotContain("PillBadgeStyle", xaml);
    }

    [Fact]
    public void Paste_queue_has_distinct_failure_surface_and_recovery_actions()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "ClipboardPasteQueueControl.xaml"));

        Assert.Contains("顺序粘贴", xaml);
        Assert.Contains("CanRecoverPasteQueue", xaml);
        Assert.Contains("BrushDangerSoft", xaml);
        Assert.Contains("PasteQueueCount", xaml);
        Assert.Contains("PrimaryButtonStyle", xaml);
        Assert.Contains("AutomationProperties.Name", xaml);
    }

    [Fact]
    public void Command_surfaces_show_keyboard_focus()
    {
        var commandXaml = File.ReadAllText(Find("src", "GestureClip.App", "QuickActionCenterWindow.xaml"));
        var queueXaml = File.ReadAllText(Find("src", "GestureClip.App", "ClipboardPasteQueueControl.xaml"));

        Assert.Contains("CommandFocusVisual", commandXaml);
        Assert.Contains("FocusVisualStyle", commandXaml);
        Assert.Contains("QueueFocusVisual", queueXaml);
        Assert.Contains("FocusVisualStyle", queueXaml);
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
