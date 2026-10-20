using System.Text.RegularExpressions;
using System.Xml.Linq;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{
    private const string HubControlsNamespace = "clr-namespace:GestureClip.App.Controls.Hub";
    private static readonly XNamespace XamlAttributeNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Hub_sections_remain_connected_to_window_and_resource_dictionaries()
    {
        var documents = LoadHubDocuments();
        var window = documents[0].Root!;
        Assert.Equal("Window", window.Name.LocalName);
        Assert.Contains(window.Elements(), element =>
            (string?)element.Attribute(XamlAttributeNamespace + "Name") == "RootBorder");

        var controls = documents.SelectMany(document => document.Descendants())
            .Where(element => element.Name.NamespaceName == HubControlsNamespace)
            .Select(element => element.Name.LocalName).ToHashSet();
        foreach (var section in new[]
        {
            "HubSetupSection", "HubEarningsSection", "HubFishingSection", "HubRestSection",
            "HubSprintSection", "HubReportSection", "HubPeriodSection", "HubRulesSection",
            "HubOverviewCard", "HubQuickPanel"
        }) Assert.Contains(section, controls);

        var dictionarySources = window.Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary")
            .Select(element => (string?)element.Attribute("Source")).ToArray();
        Assert.Contains("Themes/HubDashboardStyles.xaml", dictionarySources);
        Assert.Contains("Themes/HubInteractionStyles.xaml", dictionarySources);
        Assert.All(documents.Where(document => document.Root?.Name.LocalName == "UserControl"),
            document => Assert.Null(document.Root!.Attribute("DataContext")));
    }

    [Fact]
    public void Hub_controls_keep_action_commands_and_work_template_context()
    {
        var controls = LoadHubDocuments().SelectMany(document => document.Descendants()).ToArray();
        var commands = controls.Select(element => BindingPath(element.Attribute("Command"))).ToHashSet();
        foreach (var command in new[]
        {
            "CompleteSetupCommand", "DismissSetupCommand", "OpenWorkRulesCommand",
            "ToggleFishingCommand", "ClearFishingCommand", "SnoozeRestReminderCommand",
            "ToggleRestReminderCommand", "DisableRestReminderCommand", "StartSprintCommand",
            "StopSprintCommand", "GenerateReportCommand", "GenerateShareCardCommand",
            "OpenLastShareCardFolderCommand", "ToggleAutoDailyReportCommand",
            "MuteDailyReportTodayCommand", "GenerateWeeklyReportCommand",
            "GenerateMonthlyReportCommand", "RefreshCommand", "ResetTodayCommand"
        }) Assert.Contains(command, commands);

        var templateButtons = controls.Where(element =>
            BindingPath(element.Attribute("Command")) == "DataContext.ApplyWorkTemplateCommand").ToArray();
        Assert.Equal(2, templateButtons.Length);
        Assert.All(templateButtons, button =>
        {
            Assert.Contains("AncestorType=Window", (string)button.Attribute("Command")!);
            Assert.Equal("{Binding}", (string?)button.Attribute("CommandParameter"));
        });
    }

    [Fact]
    public void Hub_controls_preserve_two_way_settings_and_expansion_bindings()
    {
        var attributes = LoadHubDocuments().SelectMany(document => document.Descendants())
            .SelectMany(element => element.Attributes()).ToArray();
        foreach (var property in new[]
        {
            "EarningsExpanded", "FishingExpanded", "RestExpanded", "SprintExpanded",
            "ReportExpanded", "PeriodExpanded", "SettingsExpanded", "RestReminderMaxPerDay",
            "RestReminderMaxPerWeek", "RestReminderMinContinuousMinutes", "WaterReminderEnabled",
            "WaterReminderIntervalMinutes", "StretchReminderEnabled", "StretchReminderIntervalMinutes",
            "ShareCardStyle", "WorkBearTextStyle", "HudFunEnabled", "GestureXpBonusEnabled"
        }) Assert.Contains(attributes, attribute => BindingPath(attribute) == property &&
            Regex.IsMatch(attribute.Value, @"\bMode\s*=\s*TwoWay\b"));

        foreach (var property in new[]
        {
            "WorkstationMonthlySalary", "WorkstationPayday", "WorkstationWorkStartTime",
            "WorkstationWorkEndTime", "WorkstationLunchStartTime", "WorkstationLunchEndTime",
            "WorkstationWorkdays"
        }) Assert.Contains(attributes, attribute => BindingPath(attribute) == property &&
            Regex.IsMatch(attribute.Value, @"\bMode\s*=\s*TwoWay\b") &&
            Regex.IsMatch(attribute.Value, @"\bUpdateSourceTrigger\s*=\s*LostFocus\b"));

        foreach (var property in new[] { "SetupSalaryText", "SetupStartTime", "SetupEndTime" })
            Assert.Contains(attributes, attribute => BindingPath(attribute) == property &&
                Regex.IsMatch(attribute.Value, @"\bUpdateSourceTrigger\s*=\s*PropertyChanged\b"));
    }

    private static IReadOnlyList<XDocument> LoadHubDocuments() => RepositoryXaml.LoadDocuments(
        FindRepositoryFile("src", "GestureClip.App", "WorkstationDashboardWindow.xaml"));

    private static string? BindingPath(XAttribute? attribute)
    {
        if (attribute is null) return null;
        var match = Regex.Match(attribute.Value, @"^\{Binding\s+(?:Path=)?([^,}\s]+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
