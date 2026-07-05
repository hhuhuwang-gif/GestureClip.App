using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public static class GestureActionCatalog
{
    public const string CommonCategory = "常用动作";
    public const string WebSearchCategory = "网页搜索";
    public const string MoreCategory = "更多动作";

    public static IReadOnlyList<GestureActionOptionViewModel> DefaultOptions { get; } =
    [
        Option(BuiltInGestureAction.None),
        Option(BuiltInGestureAction.Copy),
        Option(BuiltInGestureAction.Paste),
        Option(BuiltInGestureAction.Cut),
        Option(BuiltInGestureAction.SelectAll),
        Option(BuiltInGestureAction.Undo),
        Option(BuiltInGestureAction.Redo),
        Option(BuiltInGestureAction.Enter),
        Option(BuiltInGestureAction.Escape),
        Option(BuiltInGestureAction.Backspace),
        Option(BuiltInGestureAction.Delete),
        Option(BuiltInGestureAction.SendAltLeft),
        Option(BuiltInGestureAction.SendAltRight),
        Option(BuiltInGestureAction.OpenClipboardOverlay),
        Option(BuiltInGestureAction.PasteAndEnter),
        Option(BuiltInGestureAction.SearchSelectedTextWithGoogle),
        Option(BuiltInGestureAction.SearchSelectedTextWithBaidu),
        Option(BuiltInGestureAction.SearchSelectedTextWithBing),
        Option(BuiltInGestureAction.OpenGoogle),
        Option(BuiltInGestureAction.OpenBaidu),
        Option(BuiltInGestureAction.PasteLatestClipboardItem),
        Option(BuiltInGestureAction.NewTab),
        Option(BuiltInGestureAction.NextTab),
        Option(BuiltInGestureAction.PreviousTab),
        Option(BuiltInGestureAction.ReopenClosedTab),
        Option(BuiltInGestureAction.Refresh),
        Option(BuiltInGestureAction.CloseTab),
        Option(BuiltInGestureAction.StartMenu),
        Option(BuiltInGestureAction.ShowDesktop),
        Option(BuiltInGestureAction.SwitchApp),
        Option(BuiltInGestureAction.TaskSwitcher),
        Option(BuiltInGestureAction.PlayPause),
        Option(BuiltInGestureAction.VolumeUp),
        Option(BuiltInGestureAction.VolumeDown),
        Option(BuiltInGestureAction.Mute),
        Option(BuiltInGestureAction.PreviousTrack),
        Option(BuiltInGestureAction.NextTrack),
        Option(BuiltInGestureAction.TaskManager),
        Option(BuiltInGestureAction.SystemSettings),
        Option(BuiltInGestureAction.Sleep),
        Option(BuiltInGestureAction.ZoomIn),
        Option(BuiltInGestureAction.ZoomOut),
        Option(BuiltInGestureAction.ResetZoom),
        Option(BuiltInGestureAction.Home),
        Option(BuiltInGestureAction.End),
        Option(BuiltInGestureAction.PageUp),
        Option(BuiltInGestureAction.PageDown),
        Option(BuiltInGestureAction.Screenshot),
        Option(BuiltInGestureAction.NextVirtualDesktop),
        Option(BuiltInGestureAction.PreviousVirtualDesktop),
        Option(BuiltInGestureAction.FullScreen),
        Option(BuiltInGestureAction.PinWindow)
    ];

    public static GestureActionOptionViewModel Option(BuiltInGestureAction action)
    {
        return new GestureActionOptionViewModel(
            action,
            GestureActionText.Name(action),
            GestureActionText.Shortcut(action),
            Category(action));
    }

    public static string Category(BuiltInGestureAction action) => action switch
    {
        BuiltInGestureAction.SearchSelectedTextWithGoogle or
        BuiltInGestureAction.SearchSelectedTextWithBaidu or
        BuiltInGestureAction.SearchSelectedTextWithBing or
        BuiltInGestureAction.OpenGoogle or
        BuiltInGestureAction.OpenBaidu => WebSearchCategory,

        BuiltInGestureAction.NewTab or
        BuiltInGestureAction.NextTab or
        BuiltInGestureAction.PreviousTab or
        BuiltInGestureAction.ReopenClosedTab or
        BuiltInGestureAction.Refresh or
        BuiltInGestureAction.CloseTab or
        BuiltInGestureAction.StartMenu or
        BuiltInGestureAction.ShowDesktop or
        BuiltInGestureAction.SwitchApp or
        BuiltInGestureAction.TaskSwitcher or
        BuiltInGestureAction.PlayPause or
        BuiltInGestureAction.VolumeUp or
        BuiltInGestureAction.VolumeDown or
        BuiltInGestureAction.Mute or
        BuiltInGestureAction.PreviousTrack or
        BuiltInGestureAction.NextTrack or
        BuiltInGestureAction.TaskManager or
        BuiltInGestureAction.SystemSettings or
        BuiltInGestureAction.Sleep or
        BuiltInGestureAction.ZoomIn or
        BuiltInGestureAction.ZoomOut or
        BuiltInGestureAction.ResetZoom or
        BuiltInGestureAction.Home or
        BuiltInGestureAction.End or
        BuiltInGestureAction.PageUp or
        BuiltInGestureAction.PageDown or
        BuiltInGestureAction.Screenshot or
        BuiltInGestureAction.NextVirtualDesktop or
        BuiltInGestureAction.PreviousVirtualDesktop or
        BuiltInGestureAction.FullScreen or
        BuiltInGestureAction.PinWindow => MoreCategory,

        _ => CommonCategory
    };
}
