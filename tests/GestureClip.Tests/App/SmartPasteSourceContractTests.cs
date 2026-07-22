using Xunit;

namespace GestureClip.Tests.App;

public sealed class SmartPasteSourceContractTests
{
    [Fact]
    public void Settings_source_contract_exposes_smart_paste_in_action_list_and_recommendations()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var viewModelPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "SettingsViewModel.cs");
        var actionTextPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureActionText.cs");
        var catalogPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureActionCatalog.cs");
        var xaml = File.ReadAllText(xamlPath);
        var viewModel = File.ReadAllText(viewModelPath);
        var actionText = File.ReadAllText(actionTextPath);
        var catalog = File.ReadAllText(catalogPath);

        Assert.Contains("RecommendedGestureBindings", xaml);
        Assert.Contains("ActionName", xaml);
        Assert.Contains("InstructionText", xaml);
        Assert.Contains("BuiltInGestureAction.SmartPaste", viewModel);
        Assert.Contains("根据当前软件自动选择", viewModel);
        Assert.Contains("IsSmartPasteEnabled", viewModel);
        Assert.Contains("SmartPasteEnabled", viewModel);
        Assert.Contains("智能粘贴", actionText);
        Assert.Contains("根据当前软件", catalog);
        Assert.Contains("智能粘贴", xaml);
        Assert.Contains("推荐", xaml);
        Assert.Contains("只净化剪贴板内容，粘贴方式与关闭时相同", xaml);
        Assert.Contains("开启智能粘贴（推荐）", xaml);  // tooltip
        Assert.Contains("统一发送 Ctrl+V", xaml);
        Assert.Contains("IsSmartPasteEnabled", xaml);
        Assert.Contains("SelectedBadgeText", xaml);
        Assert.Contains("DeleteCommand", xaml);
        Assert.Contains("ApplyRecommendedGestureBindingsCommand", xaml);
    }

    [Fact]
    public void Settings_gesture_card_source_contract_exposes_left_button_modifier_rows()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var viewModelPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureBindingCardViewModel.cs");
        var xaml = File.ReadAllText(xamlPath);
        var viewModel = File.ReadAllText(viewModelPath);

        Assert.Contains("普通动作", xaml);
        Assert.Contains("点左键增强", xaml);
        Assert.Contains("暂无增强动作", xaml);
        Assert.Contains("PrimaryActionLabel", xaml);
        Assert.Contains("PrimaryActionValueText", xaml);
        Assert.Contains("LeftButtonModifierLabel", xaml);
        Assert.Contains("LeftButtonModifierValueText", xaml);
        Assert.Contains("LeftButtonModifierBadgeText", xaml);
        Assert.Contains("普通动作", viewModel);
        Assert.Contains("点左键增强", viewModel);
        Assert.Contains("暂无增强动作", viewModel);
    }

    [Fact]
    public void Settings_home_source_contract_exposes_feature_map_for_new_users()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("FeatureMapCard", xaml);
        Assert.Contains("FeatureMapItemCardStyle", xaml);
        Assert.Contains("我能帮你做什么", xaml);
        Assert.Contains("先看这些入口", xaml);
        Assert.Contains("智能粘贴", xaml);
        Assert.Contains("鼠标手势", xaml);
        Assert.Contains("左键增强 / 设计动作", xaml);
        Assert.Contains("剪贴板历史", xaml);
        Assert.Contains("快捷入口", xaml);
        Assert.Contains("本地安全", xaml);
        Assert.Contains("FeatureMapCard_MouseLeftButtonUp", xaml); // navigation via click
        Assert.Contains("打开手势页", xaml);
        Assert.Contains("打开剪贴板页", xaml);
        Assert.Contains("打开隐私页", xaml);
        Assert.Contains("<UniformGrid Columns=\"2\"", xaml);
    }

    private static string FindRepositoryFile(params string[] segments)
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

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
