using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GestureClip.App.Controls;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class SmoothScrollViewerTests
{
    // Relative input times from the user's 2026-10-24 action-binding trace.
    private static readonly double[] RecordedBurstTimes = [0, 15.841, 36.180, 45.450, 67.870, 83.903, 120.346, 180.148];

    [Fact]
    public async Task Recorded_fast_wheel_burst_keeps_advancing_before_input_ends()
        => await RunOnStaThread(() =>
        {
            var viewer = CreateViewer(new Border { Height = 4000 });
            var window = new Window { Content = viewer, Width = 400, Height = 400, ShowActivated = false };
            try
            {
                window.Show();
                SampleOffsets(viewer);
                var requests = new List<double>();
                viewer.FrameLogging = true;
                viewer.DiagnosticLog = line => CollectFrame(line, requests);
                var duringInput = ReplayBurst(viewer);
                var lines = SystemParameters.WheelScrollLines;
                var step = lines < 0 ? viewer.ViewportHeight : lines * 16;
                Assert.Equal(RecordedBurstTimes.Length * step, viewer.VerticalOffset, 1);
                if (!SystemParameters.ClientAreaAnimation || step == 0) return;
                Assert.True(duringInput > 0);
                Assert.True(requests.Count > 2);
                Assert.True(requests.Zip(requests.Skip(1), (first, next) => next > first).All(value => value),
                    string.Join(", ", requests));
            }
            finally { window.Close(); }
        });

    private static double ReplayBurst(SmoothScrollViewer viewer)
    {
        const int settleMilliseconds = 500;
        var frame = new DispatcherFrame();
        var elapsed = Stopwatch.StartNew();
        var next = 0;
        var duringInput = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(2) };
        timer.Tick += (_, _) =>
        {
            while (next < RecordedBurstTimes.Length && elapsed.Elapsed.TotalMilliseconds >= RecordedBurstTimes[next])
            {
                duringInput = viewer.VerticalOffset;
                Wheel(viewer, -120);
                next++;
            }
            if (elapsed.Elapsed.TotalMilliseconds >= RecordedBurstTimes[^1] + settleMilliseconds) frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        return duringInput;
    }

    private static void CollectFrame(string line, List<double> requests)
    {
        if (line.StartsWith("frame desired=", StringComparison.Ordinal))
            requests.Add(double.Parse(line.Split(' ')[1].Split('=')[1], CultureInfo.CurrentCulture));
    }
}
