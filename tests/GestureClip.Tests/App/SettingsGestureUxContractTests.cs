using Xunit;

namespace GestureClip.Tests.App;

public sealed class SettingsGestureUxContractTests
{
    [Fact]
    public void SettingsWindow_wires_change_action_to_editor_and_navigation_hooks()
    {
        var xaml = File.ReadAllText(Find("src", "GestureClip.App", "SettingsWindow.xaml"));
        var code = File.ReadAllText(Find("src", "GestureClip.App", "SettingsWindow.xaml.cs"));

        Assert.Contains("MainSettingsTabControl", xaml);
        Assert.Contains("Click=\"ChangeGestureAction_Click\"", xaml);
        Assert.Contains("Tag=\"{Binding Pattern}\"", xaml);
        Assert.Contains("NavigateToBindings_Click", xaml);
        Assert.Contains("NavigateToEdgeEnhancement_Click", xaml);
        Assert.Contains("ExpandGestureAdvanced_Click", xaml);
        Assert.Contains("EdgeEnhancementPromoCard", xaml);
        Assert.Contains("GestureAdvancedSettingsExpander", xaml);
        Assert.Contains("EdgeTriggerSettingsGroup", xaml);
        Assert.Contains("FeatureMapCard_MouseLeftButtonUp", xaml);
        Assert.Contains("在此选择新动作", xaml);

        Assert.Contains("void ChangeGestureAction_Click", code);
        Assert.Contains("void NavigateToPage(string page", code);
        Assert.Contains("ScrollToNamedElement", code);
        Assert.Contains("SectionGestureEditor", code);
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
        var life = File.ReadAllText(Find("src", "GestureClip.App", "Services", "AppLifecycleService.cs"));
        var iface = File.ReadAllText(Find("src", "GestureClip.Core", "Abstractions", "IAppLifecycleService.cs"));

        Assert.Contains("ShowSettingsWindow(string? page = null)", life);
        Assert.Contains("NavigateToPage(page)", life);
        Assert.Contains("ShowSettingsWindow(string? page = null)", iface);
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
