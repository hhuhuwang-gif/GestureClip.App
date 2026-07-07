using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public sealed class RecommendedGestureBindingViewModel(
    string pattern,
    string directionText,
    string gestureName,
    BuiltInGestureAction action,
    string instructionText)
{
    public string Pattern { get; } = pattern;

    public string DirectionText { get; } = directionText;

    public string ShortDirectionText => string.Equals(Pattern, "R+L", StringComparison.Ordinal)
        ? "R+L"
        : DirectionText;

    public string GestureName { get; } = gestureName;

    public BuiltInGestureAction Action { get; } = action;

    public string ActionName => GestureActionText.Name(Action);

    public string PatternText => $"手势码：{Pattern}";

    public string InstructionText { get; } = instructionText;
}
