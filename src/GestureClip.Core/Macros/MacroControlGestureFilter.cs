namespace GestureClip.Core.Macros;

public static class MacroControlGestureFilter
{
    public static IReadOnlyList<MacroEvent> Exclude(IReadOnlyList<MacroEvent> events, DateTimeOffset recordingStartedAt, MacroGestureInterval gesture)
    {
        if (gesture.StartedAt > gesture.EndedAt) throw new ArgumentException("手势时间顺序无效。", nameof(gesture));
        var start = (gesture.StartedAt - recordingStartedAt).Ticks;
        var end = (gesture.EndedAt - recordingStartedAt).Ticks;
        var down = FindLastDown(events, start);
        if (down < 0) return events; // Gestures inside the recorder window are already excluded.
        var up = FindMatchingUp(events, down, end);
        if (up < 0 || events[up].OffsetTicks < start) return events;
        return events.Where((item, index) => index < down || index > up || IsKeyboard(item)).ToArray();
    }

    private static int FindLastDown(IReadOnlyList<MacroEvent> events, long beforeTicks)
    {
        for (var index = events.Count - 1; index >= 0; index--)
            if (events[index].Kind == MacroEventKind.RightDown && events[index].OffsetTicks <= beforeTicks) return index;
        return -1;
    }

    private static int FindMatchingUp(IReadOnlyList<MacroEvent> events, int down, long beforeTicks)
    {
        for (var index = down + 1; index < events.Count; index++)
        {
            if (events[index].OffsetTicks > beforeTicks) return -1;
            if (events[index].Kind == MacroEventKind.RightUp) return index;
        }
        return -1;
    }

    private static bool IsKeyboard(MacroEvent item) => item.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp;
}
