using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GestureClip.App.ViewModels;

/// <summary>
/// Minimal ObservableCollection extension used only by the clipboard overlay to commit a
/// full page of search results with a single Reset notification instead of one
/// CollectionChanged per item. Indexer/replace/remove behavior is inherited unchanged.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceRange(IEnumerable<T> items)
    {
        var buffer = new List<T>(items);
        if (buffer.Count == 0)
        {
            Clear();
            return;
        }

        Items.Clear();
        foreach (var item in buffer)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}