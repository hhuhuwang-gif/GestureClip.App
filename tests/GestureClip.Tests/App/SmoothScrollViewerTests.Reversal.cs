using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GestureClip.App.Controls;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class SmoothScrollViewerTests
{
    [Fact]
    public async Task Fast_reverse_wheel_keeps_distance_accumulated_in_the_other_direction()
        => await RunOnStaThread(() =>
        {
            var viewer = CreateViewer(new Border { Height = 4000 });
            var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
            try
            {
                window.Show();
                SampleOffsets(viewer);
                var step = WheelStep(viewer);
                if (step <= 0) return;

                // Three notches down, then one notch up before the animation settles.
                ReplayWheel(viewer, [-120, -120, -120, 120], intervalMilliseconds: 10);
                SampleOffsets(viewer);

                // Down three notches and up one notch must land on two notches of travel.
                Assert.Equal(2 * step, viewer.VerticalOffset, 0);
            }
            finally { window.Close(); }
        });

    [Fact]
    public async Task Reversing_wheel_never_moves_against_the_requested_direction()
        => await RunOnStaThread(() =>
        {
            var viewer = CreateViewer(new Border { Height = 4000 });
            var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
            try
            {
                window.Show();
                SampleOffsets(viewer);
                var step = WheelStep(viewer);
                if (step <= 0) return;

                var offsets = new List<double>();
                viewer.ScrollChanged += (_, e) => offsets.Add(e.VerticalOffset);
                ReplayWheel(viewer, [-120, -120, -120, 120], intervalMilliseconds: 10);

                // The down run must peak before the up wheel moves back a single step.
                var peak = offsets.Count == 0 ? 0 : offsets.Max();
                Assert.True(peak >= 2 * step, $"peak={peak:F2} step={step:F2}");
                Assert.True(viewer.VerticalOffset >= 1 * step, $"final={viewer.VerticalOffset:F2}");
            }
            finally { window.Close(); }
        });

    [Fact]
    public async Task Alternating_wheel_does_not_jump_back_and_forth()
        => await RunOnStaThread(() =>
        {
            var viewer = CreateViewer(new Border { Height = 4000 });
            var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
            try
            {
                window.Show();
                SampleOffsets(viewer);

                // Net zero input: every notch is answered by the opposite one.
                var frameOffsets = new List<double>();
                viewer.ScrollChanged += (_, e) => frameOffsets.Add(e.VerticalOffset);
                ReplayWheel(viewer,
                    [-120, -120, 120, 120, -120, -120, 120, 120],
                    intervalMilliseconds: 10);

                Assert.True(viewer.VerticalOffset < 2, $"final={viewer.VerticalOffset:F2}");
                var span = frameOffsets.Count == 0 ? 0 : frameOffsets.Max() - frameOffsets.Min();
                Assert.True(span < WheelStep(viewer) * 4, $"span={span:F2}");
            }
            finally { window.Close(); }
        });

    private static double WheelStep(SmoothScrollViewer viewer)
    {
        var lines = SystemParameters.WheelScrollLines;
        return lines < 0 ? viewer.ViewportHeight : lines * 16;
    }

    private static void ReplayWheel(SmoothScrollViewer viewer, int[] deltas, int intervalMilliseconds)
    {
        var frame = new DispatcherFrame();
        var sent = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(intervalMilliseconds)
        };
        var finish = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(deltas.Length * intervalMilliseconds + 500)
        };
        timer.Tick += (_, _) =>
        {
            Wheel(viewer, deltas[sent]);
            if (++sent == deltas.Length) timer.Stop();
        };
        finish.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        finish.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        finish.Stop();
    }
}
