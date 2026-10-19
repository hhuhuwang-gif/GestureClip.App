using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Assistant;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;


public sealed partial class ClipboardOverlayViewModel
{
    private void MarkItemsUsed(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var uniqueIds = ids.ToHashSet();
        foreach (var id in uniqueIds)
        {
            _sessionCopiedIds.Add(id);
        }

        var now = DateTimeOffset.UtcNow;
        var updatedById = new Dictionary<Guid, ClipboardItem>(uniqueIds.Count);
        _lastSearchResults = _lastSearchResults
            .Select(item =>
            {
                if (!uniqueIds.Contains(item.Id))
                {
                    return item;
                }

                var updated = item with
                {
                    UseCount = item.UseCount + 1,
                    LastUsedAt = now,
                    UpdatedAt = now,
                    IsSessionCopied = true
                };
                updatedById[item.Id] = updated;
                return updated;
            })
            .ToArray();

        ClipboardItem? selectedUpdate = null;
        for (var index = 0; index < Items.Count; index++)
        {
            if (!updatedById.TryGetValue(Items[index].Id, out var updated))
            {
                continue;
            }

            Items[index] = updated;
            if (SelectedItem?.Id == updated.Id)
            {
                selectedUpdate = updated;
            }
        }

        if (selectedUpdate is not null)
        {
            SelectedItem = selectedUpdate;
        }

        OnPropertyChanged(nameof(SummaryText));
    }

    private ClipboardItem WithSessionCopiedFlag(ClipboardItem item)
    {
        var copied = _sessionCopiedIds.Contains(item.Id);
        return item.IsSessionCopied == copied ? item : item with { IsSessionCopied = copied };
    }

    private void UpdateItem(Guid id, Func<ClipboardItem, ClipboardItem> update)
    {
        _lastSearchResults = _lastSearchResults
            .Select(item => item.Id == id ? update(item) : item)
            .ToArray();

        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].Id != id)
            {
                continue;
            }

            var updated = update(Items[index]);
            Items[index] = updated;
            if (SelectedItem?.Id == id)
            {
                SelectedItem = updated;
            }

            break;
        }

        OnPropertyChanged(nameof(SummaryText));
    }

    private void RemoveItems(IReadOnlyList<Guid> ids)
    {
        var idSet = ids.ToHashSet();
        _lastSearchResults = _lastSearchResults
            .Where(item => !idSet.Contains(item.Id))
            .ToArray();

        var firstDeletedIndex = -1;
        for (var index = 0; index < Items.Count; index++)
        {
            if (idSet.Contains(Items[index].Id))
            {
                firstDeletedIndex = index;
                break;
            }
        }

        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (idSet.Contains(Items[index].Id))
            {
                Items.RemoveAt(index);
            }
        }

        if (Items.Count == 0)
        {
            SelectedItem = null;
            UpdateSelectedCount(0);
        }
        else if (firstDeletedIndex >= 0)
        {
            var nextIndex = Math.Min(firstDeletedIndex, Items.Count - 1);
            SelectedItem = Items[nextIndex];
            UpdateSelectedCount(1);
        }
        else
        {
            SelectedItem = Items.FirstOrDefault();
            UpdateSelectedCount(SelectedItem is null ? 0 : 1);
        }

        EmptyStateText = Items.Count == 0 ? "没有匹配的剪贴板记录" : "";
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateHint));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
