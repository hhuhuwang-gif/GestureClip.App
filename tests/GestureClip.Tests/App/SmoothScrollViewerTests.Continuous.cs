using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class SmoothScrollViewerTests
{
    [Theory]
    [InlineData(-120)]
    [InlineData(120)]
    public async Task Restart_never_submits_old_base_offset_before_new_animation_starts(int nextDelta)
        => await RunOnStaThread(() =>
        {
            var viewer = CreateViewer(new Border { Height = 4000 });
            Wheel(viewer, -120);
            SampleOffsets(viewer);
            var start = viewer.VerticalOffset;
            var trace = new List<string>();
            viewer.FrameLogging = true;
            viewer.DiagnosticLog = trace.Add;
            Wheel(viewer, nextDelta);
            SampleOffsets(viewer);
            var requests = trace.Where(line => line.StartsWith("frame desired="))
                .Select(line => double.Parse(line.Split(' ')[1].Split('=')[1],
                    System.Globalization.CultureInfo.CurrentCulture)).ToArray();
            if (SystemParameters.ClientAreaAnimation && start > 0) Assert.NotEmpty(requests);
            var sequence = requests.Prepend(start).ToArray();
            Assert.True(sequence.Zip(sequence.Skip(1), (first, next) =>
                nextDelta < 0 ? next >= first : next <= first).All(value => value), string.Join("; ", trace));
        });

    [Theory]
    [InlineData(15, 0)]
    [InlineData(30, 25)]
    [InlineData(110, 0)]
    [InlineData(125, 25)]
    public async Task Continuous_wheel_input_never_reverses_direction_between_frames(int interval, int busyMilliseconds)
        => await RunOnStaThread(() =>
        {
            var viewer = CreateViewer(new Border { Height = 4000 });
            var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
            try
            {
                window.Show();
                SampleOffsets(viewer);
                var offsets = new List<double>();
                viewer.ScrollChanged += (_, e) => offsets.Add(e.VerticalOffset);
                SendContinuousWheel(viewer, interval, busyMilliseconds);
                Assert.True(offsets.Count > 2);
                Assert.True(offsets.Zip(offsets.Skip(1), (first, next) => next >= first).All(value => value),
                    string.Join(", ", offsets.Select(offset => offset.ToString("F2"))));
            }
            finally { window.Close(); }
        });

    private static void SendContinuousWheel(GestureClip.App.Controls.SmoothScrollViewer viewer, int interval, int busyMilliseconds)
    {
        const int inputCount = 8;
        var frame = new DispatcherFrame();
        var sent = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(interval) };
        var finish = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(inputCount * interval + 400) };
        timer.Tick += (_, _) =>
        {
            Wheel(viewer, -120);
            if (busyMilliseconds > 0) Thread.Sleep(busyMilliseconds);
            if (++sent == inputCount) timer.Stop();
        };
        finish.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        finish.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        finish.Stop();
    }
}
