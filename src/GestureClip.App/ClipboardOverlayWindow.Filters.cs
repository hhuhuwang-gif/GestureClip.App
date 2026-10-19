using System.Windows.Controls;
using GestureClip.App.Services;
using RadioButton = System.Windows.Controls.RadioButton;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow
{
    private bool TryExecuteFilterShortcut(ClipboardShortcut shortcut)
    {
        switch (shortcut.Action)
        {
            case ClipboardShortcutAction.SelectFilter: SelectFilterByShortcut(shortcut.Index); break;
            case ClipboardShortcutAction.NextFilter: CycleFilter(true); break;
            case ClipboardShortcutAction.PreviousFilter: CycleFilter(false); break;
            default: return false;
        }
        return true;
    }

    private RadioButton[] GetFilterButtons() =>
        [AllFilterButton, PinnedFilterButton, FavoritesFilterButton, TextFilterButton, ImagesFilterButton, LinksFilterButton];

    private void SelectFilterByShortcut(int index)
    {
        GetFilterButtons()[index].IsChecked = true;
    }

    private void CycleFilter(bool forward)
    {
        var filters = GetFilterButtons();
        var currentIndex = Array.FindIndex(filters, filter => filter.IsChecked == true);
        var step = forward ? 1 : -1;
        var nextIndex = (currentIndex + step + filters.Length) % filters.Length;
        filters[nextIndex].IsChecked = true;
    }
}
