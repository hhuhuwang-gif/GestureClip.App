using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GestureClip.App.Controls;
using GestureClip.Tests.Support;
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
        await completion.Task.WaitAsync(TestPlatformTimeouts.UiThread);
    }

    private static void VerifyWheelScrolling()
    {
        var viewer = CreateViewer(new Border { Height = 4000, Width = 300 });
        var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
        try
        {
            // 放进窗口并先等布局落定：没有渲染源时 ScrollViewer 的 extent/viewport 会被后续
            // 布局重新计算，Takeover 逻辑随即把已经滚出去的行程吞掉，最终偏移就正好少一档。
            window.Show();
            viewer.UpdateLayout();
            PumpUntilSettled(viewer);

            Wheel(viewer, -120);
            Wheel(viewer, -120);
            var offsets = SampleOffsets(viewer);
            PumpUntilSettled(viewer);
            var lines = SystemParameters.WheelScrollLines;
            var expected = 2 * (lines < 0 ? viewer.ViewportHeight : lines * 16);
            Assert.Equal(expected, viewer.VerticalOffset, 1);
            if (SystemParameters.ClientAreaAnimation && expected > 0)
                Assert.Contains(offsets, offset => offset > 0 && offset < expected);
            Wheel(viewer, 120);
            var upwardOffsets = SampleOffsets(viewer);
            PumpUntilSettled(viewer);
            Assert.Equal(expected / 2, viewer.VerticalOffset, 1);
            Assert.True(upwardOffsets.Zip(upwardOffsets.Skip(1), (first, next) => next <= first).All(value => value));
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// 平滑滚动由 CompositionTarget.Rendering 驱动，慢机器上帧率低，采样结束时可能还差一档没落位。
    /// 断言最终偏移前先泵帧到位置稳定，避免把“还没滑完”误判成“行程丢失”。
    /// </summary>
    private static void PumpUntilSettled(SmoothScrollViewer viewer, int maxMilliseconds = 4000)
    {
        var frame = new DispatcherFrame();
        var elapsed = Stopwatch.StartNew();
        var last = double.NaN;
        var stableTicks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            viewer.UpdateLayout();
            var current = viewer.VerticalOffset;
            stableTicks = Math.Abs(current - last) < 0.5 ? stableTicks + 1 : 0;
            last = current;
            if (stableTicks >= 4 || elapsed.ElapsedMilliseconds >= maxMilliseconds) frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
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
