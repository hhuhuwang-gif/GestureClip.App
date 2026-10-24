using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GestureClip.App.Controls;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class SmoothScrollViewerTests
{
    [Fact]
    public async Task Wheel_scroll_has_intermediate_frames_and_keeps_accumulated_distance()
        => await RunOnStaThread(VerifyWheelScrolling);

    [Fact]
    public async Task BringIntoView_during_wheel_animation_keeps_requested_destination()
        => await RunOnStaThread(() =>
        {
            var destination = new Border { Height = 100 };
            var content = new StackPanel();
            content.Children.Add(new Border { Height = 3000 });
            content.Children.Add(destination);
            var viewer = CreateViewer(content);
            var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
            try
            {
                window.Show();
                viewer.UpdateLayout();
                SampleOffsets(viewer);
                destination.BringIntoView(new Rect(0, 0, destination.ActualWidth, destination.ActualHeight));
                SampleOffsets(viewer);
                Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset, 1);
                viewer.ScrollToTop();
                SampleOffsets(viewer);
                Wheel(viewer, -120);
                destination.BringIntoView(new Rect(0, 0, destination.ActualWidth, destination.ActualHeight));
                SampleOffsets(viewer);
                Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset, 1);
            }
            finally { window.Close(); }
        });

    private static async Task RunOnStaThread(Action assertion)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { assertion(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void VerifyWheelScrolling()
    {
        var viewer = CreateViewer(new Border { Height = 4000, Width = 300 });
        Wheel(viewer, -120);
        Wheel(viewer, -120);
        var offsets = SampleOffsets(viewer);
        var lines = SystemParameters.WheelScrollLines;
        var expected = 2 * (lines < 0 ? viewer.ViewportHeight : lines * 16);
        Assert.Equal(expected, viewer.VerticalOffset, 1);
        if (SystemParameters.ClientAreaAnimation && expected > 0)
            Assert.Contains(offsets, offset => offset > 0 && offset < expected);
        Wheel(viewer, 120);
        var upwardOffsets = SampleOffsets(viewer);
        Assert.Equal(expected / 2, viewer.VerticalOffset, 1);
        Assert.True(upwardOffsets.Zip(upwardOffsets.Skip(1), (first, next) => next <= first).All(value => value));
    }

    private static SmoothScrollViewer CreateViewer(UIElement content)
    {
        var viewer = new SmoothScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        viewer.Measure(new System.Windows.Size(400, 400));
        viewer.Arrange(new Rect(0, 0, 400, 400));
        viewer.UpdateLayout();
        return viewer;
    }

    private static void Wheel(SmoothScrollViewer viewer, int delta) => viewer.RaiseEvent(
        new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = Mouse.MouseWheelEvent
        });

    private static List<double> SampleOffsets(SmoothScrollViewer viewer)
    {
        var offsets = new List<double>();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        const int sampleCount = 20;
        timer.Tick += (_, _) =>
        {
            viewer.UpdateLayout();
            offsets.Add(viewer.VerticalOffset);
            if (offsets.Count >= sampleCount) frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        return offsets;
    }
}
