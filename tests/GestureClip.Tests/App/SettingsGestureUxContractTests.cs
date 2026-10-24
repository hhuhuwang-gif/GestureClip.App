using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed class SettingsGestureUxContractTests
{
    [Fact]
    public void SettingsWindow_wires_change_action_to_editor_and_navigation_hooks()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "SettingsWindow.xaml"));
        var code = ReadAllMatching("src", "GestureClip.App", "SettingsWindow*.cs");

        Assert.Contains("MainSettingsTabControl", xaml);
        Assert.Contains("ChangeActionRequested=\"ChangeGestureAction_Click\"", xaml);
        Assert.Contains("Tag=\"{Binding Pattern}\"", xaml);
        Assert.Contains("NavigateToBindings_Click", xaml);
        Assert.Contains("NavigateToEdgeEnhancement_Click", xaml);
        Assert.Contains("ExpandGestureAdvanced_Click", xaml);
        Assert.Contains("EdgeEnhancementPromoCard", xaml);
        Assert.Contains("GestureAdvancedSettingsExpander", xaml);
        Assert.Contains("EdgeTriggerSettingsGroup", xaml);
        Assert.Contains("FeatureMapCard_MouseLeftButtonUp", xaml);
        Assert.Contains("BindingActionPopup", xaml);
        Assert.Contains("GestureActionPicker", xaml);

        Assert.Contains("void ChangeGestureAction_Click", code);
        Assert.Contains("void NavigateToPage(string page", code);
        Assert.Contains("ScrollToNamedElement", code);
        Assert.Contains("SectionGestureEditor", code);
    }

    [Fact]
    public void Changing_action_does_not_queue_competing_scroll_requests()
    {
        var code = File.ReadAllText(Find("src", "GestureClip.App", "SettingsWindow.Navigation.cs"));
        var start = code.IndexOf("private void ChangeGestureAction_Click", StringComparison.Ordinal);
        var end = code.IndexOf("private void ScrollToCustomGestureDesigner_Click", start, StringComparison.Ordinal);
        var handler = code[start..end];

        Assert.DoesNotContain("BringIntoView", handler);
        Assert.DoesNotContain("ScrollToNamedElement", handler);
        Assert.DoesNotContain("NavigateToPage(\"bindings\",", handler);
        Assert.DoesNotContain("ScrollToVerticalOffset", handler);
        Assert.Contains("BindingActionPopup.IsOpen = true", handler);
    }

    [Fact]
    public void Moving_or_resizing_settings_never_restores_whole_window_shadow()
    {
        var code = File.ReadAllText(Find("src", "GestureClip.App", "SettingsWindow.Resize.cs"));
        Assert.DoesNotContain("new DropShadowEffect", code);
        Assert.DoesNotContain("SetWindowShadowEnabled", code);
        Assert.Contains("WmNcHitTest", code);
    }

    [Fact]
    public void Selecting_gesture_cards_does_not_change_their_border_geometry()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "SettingsWindow.xaml"));

        Assert.DoesNotContain("TargetName=\"GestureCard\" Property=\"BorderThickness\" Value=\"2\"", xaml);
    }

    [Fact]
    public void Onboarding_feature_cards_jump_into_settings_pages()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "OnboardingWindow.xaml"));
        var code = File.ReadAllText(Find("src", "GestureClip.App", "OnboardingWindow.xaml.cs"));

        Assert.Contains("FeatureCard_MouseLeftButtonUp", xaml);
        Assert.Contains("Tag=\"clipboard\"", xaml);
        Assert.Contains("Tag=\"bindings\"", xaml);
        Assert.Contains("Tag=\"workbear\"", xaml);
        Assert.Contains("ShowSettingsWindow(page)", code);
        Assert.Contains("点击去设计动作", xaml);
    }

    [Fact]
    public void AppLifecycle_accepts_settings_page_argument()
    {
        var life = RepositorySource.ReadClassSources(Find("src", "GestureClip.App", "Services", "AppLifecycleService.cs"));
        var iface = File.ReadAllText(Find("src", "GestureClip.Core", "Abstractions", "IAppLifecycleService.cs"));

        Assert.Contains("ShowSettingsWindow(string? page = null)", life);
        Assert.Contains("NavigateToPage(page)", life);
        Assert.Contains("ShowSettingsWindow(string? page = null)", iface);
        Assert.Contains("ShowGestureBindingEditor", iface);
        Assert.Contains("ShowGestureBindingEditor", life);
    }


    private static string ReadAllMatching(params string[] pathAndGlob)
    {
        if (pathAndGlob.Length < 1)
        {
            throw new ArgumentException("Need at least a relative path segment.");
        }

        var glob = pathAndGlob[^1];
        var dirSegments = pathAndGlob[..^1];
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateDir = Path.Combine(new[] { directory.FullName }.Concat(dirSegments).ToArray());
            if (Directory.Exists(candidateDir))
            {
                var files = Directory.GetFiles(candidateDir, glob)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (files.Length == 0)
                {
                    throw new FileNotFoundException("No files matched.", Path.Combine(candidateDir, glob));
                }

                return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(string.Join("/", dirSegments));
    }

    private static string Find(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join("/", segments));
    }
}
