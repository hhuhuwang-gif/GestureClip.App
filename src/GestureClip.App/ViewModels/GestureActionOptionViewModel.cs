using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public sealed record GestureActionOptionViewModel(BuiltInGestureAction Action, string Name, string Shortcut, string Category)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Shortcut)
        ? Name
        : $"{Name}  ·  {Shortcut}";

    public override string ToString() => DisplayName;
}
