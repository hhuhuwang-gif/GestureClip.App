using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using GestureClip.App.Controls;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ToggleSwitchMotionTests
{
    [Fact]
    public async Task Frozen_template_transforms_are_cloned_per_switch_and_keep_correct_state()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { VerifyTemplateInstances(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void VerifyTemplateInstances()
    {
        var template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options"
                             TargetType="CheckBox">
                <Ellipse x:Name="Thumb" Width="20" Height="20">
                    <Ellipse.RenderTransform><TranslateTransform po:Freeze="True" /></Ellipse.RenderTransform>
                </Ellipse>
            </ControlTemplate>
            """);
        var first = new CheckBox { Template = template, IsChecked = true };
        var second = new CheckBox { Template = template, IsChecked = false };
        ToggleSwitchMotion.SetIsEnabled(first, true);
        ToggleSwitchMotion.SetIsEnabled(second, true);
        var panel = new StackPanel();
        panel.Children.Add(first);
        panel.Children.Add(second);
        var window = new Window { Content = panel, Width = 100, Height = 100, ShowInTaskbar = false };
        try
        {
            window.Show();
            var firstTransform = TransformOf(first);
            var secondTransform = TransformOf(second);
            Assert.False(firstTransform.IsFrozen);
            Assert.False(secondTransform.IsFrozen);
            Assert.NotSame(firstTransform, secondTransform);
            Assert.Equal(18d, firstTransform.X);
            Assert.Equal(0d, secondTransform.X);
            first.IsChecked = false;
            second.IsChecked = true;
            Assert.Equal(0d, firstTransform.GetAnimationBaseValue(TranslateTransform.XProperty));
            Assert.Equal(18d, secondTransform.GetAnimationBaseValue(TranslateTransform.XProperty));
        }
        finally { window.Close(); }
    }

    private static TranslateTransform TransformOf(CheckBox toggle) =>
        (TranslateTransform)((FrameworkElement)toggle.Template.FindName("Thumb", toggle)).RenderTransform;
}
