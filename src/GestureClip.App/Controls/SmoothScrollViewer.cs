using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace GestureClip.App.Controls;

/// <summary>
/// ScrollViewer with a smooth wheel animation that never bounces.
/// </summary>
/// <remarks>
/// Wheel input is accumulated into a single target offset. Every rendered frame
/// moves the viewport towards that target with an exponential approach, but the
/// step is always monotonic and clamped to the target so the content can never
/// overshoot and spring back.
///
/// Reversing the wheel keeps the distance that was already requested: three
/// notches down followed by one notch up ends exactly two notches down. The
/// previous implementation restarted from the lagging visual position on every
/// direction change, which dropped a full notch of travel and looked like a
/// bounce.
///
/// Any scroll that does not originate from this control (scrollbar drag,
/// keyboard, BringIntoView, code) immediately takes over the target instead of
/// fighting it.
/// </remarks>
public sealed class SmoothScrollViewer : ScrollViewer
{
    private const double WheelDeltaPerNotch = 120.0;
    private const double LineHeight = 16.0;

    // Exponential time constant. Short enough to track a fast wheel burst,
    // long enough to smooth the discrete 120-delta steps.
    private const double ResponseSeconds = 0.026;

    // Distance at which the animation is considered settled. A whole pixel is
    // invisible to the eye and saves the last few layout passes of every scroll.
    private const double SettleEpsilon = 1.0;

    // A viewport offset this far from what we submitted was not caused by us.
    private const double ExternalChangeEpsilon = 1.0;

    // Floor for the frame delta so a burst of render callbacks cannot make the
    // approach step collapse to zero and keep the animation alive.
    private const double MinFrameMilliseconds = 6.0;

    private double _targetOffset;
    private double _visualOffset;
    private double _submittedOffset;
    private bool _hasSubmitted;
    private bool _isRunning;
    private long _lastTick;
    private TimeSpan? _lastRenderingTime;
    private int _frameCount;
    private double _frameTotalMilliseconds;
    private double _frameMaxMilliseconds;
    private bool DiagnosticsEnabled => DiagnosticLog is not null;
    private double _submitMaxMilliseconds;

    public Action<string>? DiagnosticLog { get; set; }

    /// <summary>
    /// Emits one log line per submitted frame. Off by default: a line per frame is
    /// synchronous log I/O on the UI thread and distorts the very timings the
    /// diagnostics are meant to measure.
    /// </summary>
    public bool FrameLogging { get; set; }

    static SmoothScrollViewer()
    {
        EventManager.RegisterClassHandler(typeof(SmoothScrollViewer), RequestBringIntoViewEvent,
            new RequestBringIntoViewEventHandler(OnContentBringIntoView), true);
    }

    public SmoothScrollViewer()
    {
        IsVisibleChanged += (_, _) => StopScrolling();
        Unloaded += (_, _) => StopScrolling();
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnScrollBarDragStarted), true);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Handled || !TryScrollByWheel(e.Delta))
        {
            base.OnMouseWheel(e);
            return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Applies one wheel delta to the accumulated target.
    /// Returns <c>false</c> when the event should keep bubbling.
    /// </summary>
    public bool TryScrollByWheel(int delta)
    {
        if (ScrollableHeight <= 0)
        {
            return false;
        }

        var step = GetWheelStep();
        var distance = -delta / WheelDeltaPerNotch * step;
        if (distance == 0 || double.IsNaN(distance))
        {
            return false;
        }

        if (!_isRunning)
        {
            ResetToViewport();
        }

        var maximum = ScrollableHeight;
        _targetOffset = Math.Clamp(_targetOffset + distance, 0, maximum);

        if (DiagnosticLog is { } wheelLog)
        {
            wheelLog($"wheel delta={delta} actual={VerticalOffset:F3} target={_targetOffset:F3} active={_isRunning}");
        }

        if (Math.Abs(_targetOffset - _visualOffset) <= SettleEpsilon)
        {
            // Already at the edge or at the target: settle without a redundant
            // layout pass when nothing would actually move.
            if (Math.Abs(_targetOffset - _visualOffset) > 0.01)
            {
                SubmitOffset(_targetOffset);
            }

            StopRunning();
            return true;
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            SubmitOffset(_targetOffset);
            return true;
        }

        StartRunning();
        return true;
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        StopRunning();
        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        StopRunning();
        base.OnPreviewMouseDown(e);
    }

    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, this))
        {
            var isOwnScroll = _hasSubmitted
                && Math.Abs(e.VerticalOffset - _submittedOffset) <= ExternalChangeEpsilon;

            if (!isOwnScroll)
            {
                // Scrollbar drag, keyboard paging, BringIntoView or code moved
                // the viewport. Adopt that position and drop the pending
                // animation instead of pulling the content back.
                StopRunning();
                _visualOffset = e.VerticalOffset;
                _targetOffset = e.VerticalOffset;
                _submittedOffset = e.VerticalOffset;
                _hasSubmitted = true;
                Log($"takeover actual={e.VerticalOffset:F3} "
                    + $"extent={e.ExtentHeight:F3} viewport={e.ViewportHeight:F3}");
            }
        }

        base.OnScrollChanged(e);
    }

    private double GetWheelStep()
    {
        var lines = SystemParameters.WheelScrollLines;
        return lines < 0 ? ViewportHeight : lines * LineHeight;
    }

    private void StartRunning()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _lastTick = Stopwatch.GetTimestamp();
        _lastRenderingTime = null;
        _frameCount = 0;
        _frameTotalMilliseconds = 0;
        _frameMaxMilliseconds = 0;
        _submitMaxMilliseconds = 0;
        CompositionTarget.Rendering += OnRendering;

        // Move once right away instead of waiting for the next rendered frame.
        // Without this the wheel input sits still for up to a full frame
        // (~17-27 ms) and then jumps, which reads as a stutter.
        Tick(MinFrameMilliseconds);
        Log("motion-start");
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var renderingTime = ((RenderingEventArgs)e).RenderingTime;
        if (_lastRenderingTime == renderingTime)
        {
            return;
        }

        _lastRenderingTime = renderingTime;

        var now = Stopwatch.GetTimestamp();
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(_lastTick, now).TotalMilliseconds;
        if (elapsedMilliseconds < MinFrameMilliseconds)
        {
            return;
        }

        Tick(elapsedMilliseconds);
    }

    private void Tick(double elapsedMilliseconds)
    {
        _lastTick = Stopwatch.GetTimestamp();
        if (DiagnosticsEnabled)
        {
            _frameCount++;
            _frameTotalMilliseconds += elapsedMilliseconds;
            if (elapsedMilliseconds > _frameMaxMilliseconds)
            {
                _frameMaxMilliseconds = elapsedMilliseconds;
            }

            if (elapsedMilliseconds > 30)
            {
                Log($"slow-frame index={_frameCount} elapsed={elapsedMilliseconds:F1}ms "
                    + $"visual={_visualOffset:F1} target={_targetOffset:F1} "
                    + $"active={_isRunning}");
            }
        }

        // Content height can change mid-flight (images, wrapping text, expanders,
        // a scrollbar appearing). Re-clamp the target instead of aborting.
        var maximum = ScrollableHeight;
        if (maximum < 0)
        {
            maximum = 0;
        }

        if (_targetOffset > maximum)
        {
            _targetOffset = maximum;
        }

        var difference = _targetOffset - _visualOffset;
        if (Math.Abs(difference) <= SettleEpsilon)
        {
            SubmitOffset(_targetOffset);
            StopRunning();
            if (DiagnosticLog is { } settleLog)
            {
                var average = _frameCount > 0 ? _frameTotalMilliseconds / _frameCount : 0;
                settleLog($"complete target={_targetOffset:F3} frames={_frameCount} "
                    + $"avg={average:F1}ms max={_frameMaxMilliseconds:F1}ms "
                    + $"layout={_submitMaxMilliseconds:F2}ms");
            }

            return;
        }

        var progress = 1 - Math.Exp(-(elapsedMilliseconds / 1000.0) / ResponseSeconds);
        if (progress > 1)
        {
            progress = 1;
        }
        else if (progress < 0)
        {
            progress = 0;
        }

        var next = _visualOffset + difference * progress;

        // Hard guard against overshoot: if the step crossed the target, snap.
        if ((_targetOffset - next) * difference <= 0)
        {
            next = _targetOffset;
        }

        SubmitOffset(next);
    }

    private void SubmitOffset(double offset)
    {
        _visualOffset = offset;
        _submittedOffset = offset;
        _hasSubmitted = true;

        if (!DiagnosticsEnabled)
        {
            ScrollToVerticalOffset(offset);
            return;
        }

        if (FrameLogging)
        {
            Log($"frame desired={offset:F3} actual={VerticalOffset:F3}");
        }

        var started = Stopwatch.GetTimestamp();
        ScrollToVerticalOffset(offset);
        var cost = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (cost > _submitMaxMilliseconds)
        {
            _submitMaxMilliseconds = cost;
        }
    }

    private void StopRunning()
    {
        if (_isRunning)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        _isRunning = false;
        _lastRenderingTime = null;
    }

    /// <summary>Drops any animation and adopts the live viewport position.</summary>
    private void StopScrolling()
    {
        StopRunning();
        ResetToViewport();
    }

    private void ResetToViewport()
    {
        var offset = VerticalOffset;
        _visualOffset = offset;
        _targetOffset = offset;
        _submittedOffset = offset;
        _hasSubmitted = false;
    }

    private void OnScrollBarDragStarted(object sender, DragStartedEventArgs e) => StopScrolling();

    private static void OnContentBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        var viewer = (SmoothScrollViewer)sender;
        if (e.TargetObject != viewer)
        {
            viewer.Log($"bring type={e.TargetObject.GetType().Name} "
                + $"name={(e.TargetObject as FrameworkElement)?.Name} actual={viewer.VerticalOffset:F3}");
            viewer.StopScrolling();
        }
    }

    private void Log(string message) => DiagnosticLog?.Invoke(message);
}
