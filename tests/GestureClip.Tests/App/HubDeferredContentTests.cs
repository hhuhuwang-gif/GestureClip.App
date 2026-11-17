using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class HubDeferredContentTests
{
    [Fact]
    public async Task Collapsed_detail_does_not_materialize_or_retain_its_visual_content()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { VerifyContentLifecycle(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void VerifyContentLifecycle()
    {
        var dictionary = ReadStyles();
        var contentTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBlock Text="detail-payload" />
            </DataTemplate>
            """);
        var detail = new Expander { Header = "Detail", Content = new object(), ContentTemplate = contentTemplate,
            Style = (Style)dictionary["BoardHubDetailStyle"] };
        var window = new Window { Content = detail, Width = 300, Height = 240, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(dictionary);
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.False(ContainsPayload(detail));
            detail.IsExpanded = true;
            window.UpdateLayout();
            Assert.True(ContainsPayload(detail));
            detail.IsExpanded = false;
            window.UpdateLayout();
            Assert.False(ContainsPayload(detail));
        }
        finally { window.Close(); }
    }

    private static bool ContainsPayload(DependencyObject element)
    {
        if (element is TextBlock { Text: "detail-payload" }) return true;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            if (ContainsPayload(VisualTreeHelper.GetChild(element, index))) return true;
        return false;
    }

    private static ResourceDictionary ReadStyles()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "GestureClip.sln"))) root = root.Parent;
        var xml = XDocument.Load(Path.Combine(root!.FullName, "src", "GestureClip.App", "Themes", "BoardPanels.xaml"));
        var dependencies = XElement.Parse("""
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Style x:Key="SecondaryButtonStyle" TargetType="Button" />
                <Style x:Key="PrimaryButtonStyle" TargetType="Button" />
                <Style x:Key="KeyboardFocusStyle" TargetType="Control" />
            </ResourceDictionary>
            """);
        xml.Root!.AddFirst(dependencies.Elements());
        return (ResourceDictionary)XamlReader.Parse(xml.ToString());
    }
}
