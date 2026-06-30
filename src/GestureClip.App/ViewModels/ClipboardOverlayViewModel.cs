using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed class ClipboardOverlayViewModel : INotifyPropertyChanged
{
    private readonly IClipboardService _clipboardService;
    private string _searchText = "";
    private ClipboardItem? _selectedItem;
    private string _emptyStateText = "";

    public ClipboardOverlayViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ClipboardItem> Items { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            _ = SearchAsync();
        }
    }

    public ClipboardItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set
        {
            _emptyStateText = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        var results = await _clipboardService.SearchAsync(SearchText, 50, CancellationToken.None);

        Items.Clear();
        foreach (var item in results)
        {
            Items.Add(item);
        }

        SelectedItem = Items.FirstOrDefault();
        EmptyStateText = Items.Count == 0 ? "没有匹配的剪贴板记录" : "";
    }

    public async Task<bool> PasteSelectedAsync()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        await _clipboardService.PasteAsync(SelectedItem, new PasteOptions(false), CancellationToken.None);
        return true;
    }

    public async Task<bool> PasteByIndexAsync(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return false;
        }

        await _clipboardService.PasteAsync(Items[index], new PasteOptions(false), CancellationToken.None);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
