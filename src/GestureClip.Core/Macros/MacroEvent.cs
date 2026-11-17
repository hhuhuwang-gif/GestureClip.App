namespace GestureClip.Core.Macros;

public enum MacroEventKind { KeyDown, KeyUp, Move, LeftDown, LeftUp, RightDown, RightUp, MiddleDown, MiddleUp, Wheel, HorizontalWheel, XDown, XUp }

public readonly record struct MacroEvent
{
    public long OffsetTicks { get; init; }
    public MacroEventKind Kind { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public uint Code { get; init; }
    public uint ScanCode { get; init; }
    public bool Extended { get; init; }
    public bool KeepCursorPosition { get; init; }

    public MacroEvent? Release() => Kind switch
    {
        MacroEventKind.KeyDown => this with { Kind = MacroEventKind.KeyUp },
        MacroEventKind.LeftDown => this with { Kind = MacroEventKind.LeftUp, KeepCursorPosition = true },
        MacroEventKind.RightDown => this with { Kind = MacroEventKind.RightUp, KeepCursorPosition = true },
        MacroEventKind.MiddleDown => this with { Kind = MacroEventKind.MiddleUp, KeepCursorPosition = true },
        MacroEventKind.XDown => this with { Kind = MacroEventKind.XUp, KeepCursorPosition = true },
        _ => null
    };
}

public sealed record RecordedMacro
{
    public int Version { get; init; } = 1;
    public string Name { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<MacroEvent> Events { get; init; } = [];

    public void Validate()
    {
        if (Version != 1 || Events is null) throw new InvalidDataException("不支持的操作宏文件格式。");
        long previous = 0;
        foreach (var item in Events)
        {
            if (!Enum.IsDefined(item.Kind) || item.OffsetTicks < previous)
                throw new InvalidDataException("操作宏包含无效事件或时间顺序。");
            if (item.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp && (item.Code > 255 || item.ScanCode > ushort.MaxValue))
                throw new InvalidDataException("操作宏包含无效键码。");
            if (item.Kind is MacroEventKind.XDown or MacroEventKind.XUp && item.Code is not (1 or 2))
                throw new InvalidDataException("操作宏包含无效鼠标侧键。");
            previous = item.OffsetTicks;
        }
    }
}
