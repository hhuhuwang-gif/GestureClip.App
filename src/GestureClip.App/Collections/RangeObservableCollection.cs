using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GestureClip.App.Collections;

/// <summary>
/// ObservableCollection with an AddRange that raises a single Reset notification,
/// so a 50-item page replace costs one CollectionChanged event instead of 50.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public void AddRange(IEnumerable<T> items)
    {
        if (items is null)
        {
            return;
        }

        using var enumerator = items.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return;
        }

        _suppressNotification = true;
        try
        {
            do
            {
                Add(enumerator.Current);
            }
            while (enumerator.MoveNext());
        }
        finally
        {
            _suppressNotification = false;
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
    }

    public void Reset(IEnumerable<T> items)
    {
        Clear();
        AddRange(items);
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotification)
        {
            return;
        }

        base.OnCollectionChanged(e);
    }
}
