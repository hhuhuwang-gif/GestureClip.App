using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed class ClipboardOverlayViewModel : INotifyPropertyChanged
{
    private readonly IClipboardService _clipboardService;
    private readonly TimeSpan _searchDebounceDelay;
    private string _searchText = "";
    private ClipboardItem? _selectedItem;
    private string _emptyStateText = "";
    private string _statusText = "";
    private CancellationTokenSource? _searchCancellation;
    private int _searchVersion;
    private int _selectedCount;
    private IReadOnlyList<ClipboardItem> _lastSearchResults = [];
    private ClipboardOverlayFilter _selectedFilter = ClipboardOverlayFilter.All;

    public ClipboardOverlayViewModel(IClipboardService clipboardService, TimeSpan? searchDebounceDelay = null)
    {
        _clipboardService = clipboardService;
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(180);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ClipboardItem> Items { get; } = [];

    public IReadOnlyList<ClipboardOverlayFilterOption> FilterOptions { get; } =
    [
        new(ClipboardOverlayFilter.All, "全部"),
        new(ClipboardOverlayFilter.Pinned, "固定"),
        new(ClipboardOverlayFilter.Favorites, "片段"),
        new(ClipboardOverlayFilter.Text, "文本"),
        new(ClipboardOverlayFilter.Images, "图片")
    ];

    public ClipboardOverlayFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (_selectedFilter == value)
            {
                return;
            }

            _selectedFilter = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

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
            _ = SearchDebouncedAsync();
        }
    }

    public ClipboardItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            _selectedItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(IsSelectedImage));
            OnPropertyChanged(nameof(IsSelectedText));
            OnPropertyChanged(nameof(SelectedContentTypeText));
            OnPropertyChanged(nameof(SelectedSourceText));
            OnPropertyChanged(nameof(SelectedCreatedAtText));
            OnPropertyChanged(nameof(SelectedUseCountText));
            OnPropertyChanged(nameof(SelectedPinActionText));
            OnPropertyChanged(nameof(SelectedFavoriteActionText));
        }
    }

    public bool HasSelectedItem => SelectedItem is not null;

    public bool IsSelectedImage => string.Equals(SelectedItem?.ContentType, "image/png", StringComparison.OrdinalIgnoreCase);

    public bool IsSelectedText => string.Equals(SelectedItem?.ContentType, "text", StringComparison.OrdinalIgnoreCase);

    public string SelectedContentTypeText => SelectedItem?.ContentType switch
    {
        "image/png" => "图片",
        "text" => "文本",
        null => "-",
        _ => SelectedItem.ContentType
    };

    public string SelectedSourceText => string.IsNullOrWhiteSpace(SelectedItem?.SourceProcess)
        ? "来源未知"
        : SelectedItem.SourceProcess!;

    public string SelectedCreatedAtText => SelectedItem is null
        ? "-"
        : SelectedItem.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string SelectedUseCountText => SelectedItem?.UseCount switch
    {
        null => "-",
        0 => "还没使用过",
        _ => $"使用 {SelectedItem.UseCount} 次"
    };

    public string SelectedPinActionText => SelectedItem?.IsPinned == true ? "取消置顶" : "置顶";

    public string SelectedFavoriteActionText => SelectedItem?.IsFavorite == true ? "取消片段" : "保存为片段";

    public string SummaryText => $"共 {Items.Count} 条 · 已选 {_selectedCount} 条";

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set
        {
            _emptyStateText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        var cancellation = ReplaceSearchCancellation();
        await SearchCoreAsync(cancellation.Token);
    }

    public async Task<bool> ClearSearchAsync()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            return false;
        }

        _searchText = "";
        OnPropertyChanged(nameof(SearchText));
        await SearchAsync();
        return true;
    }

    private async Task SearchDebouncedAsync()
    {
        var cancellation = ReplaceSearchCancellation();
        try
        {
            if (_searchDebounceDelay > TimeSpan.Zero)
            {
                await Task.Delay(_searchDebounceDelay, cancellation.Token);
            }

            await SearchCoreAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchCoreAsync(CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref _searchVersion);
        IReadOnlyList<ClipboardItem> results;
        try
        {
            results = await _clipboardService.SearchAsync(SearchText, 50, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || version != Volatile.Read(ref _searchVersion))
        {
            return;
        }

        _lastSearchResults = results;
        ApplyFilter();
        StatusText = "";
    }

    private void ApplyFilter()
    {
        var filtered = _lastSearchResults.Where(MatchesSelectedFilter).ToArray();
        Items.Clear();
        foreach (var item in filtered)
        {
            Items.Add(item);
        }

        SelectedItem = Items.FirstOrDefault();
        UpdateSelectedCount(SelectedItem is null ? 0 : 1);
        EmptyStateText = Items.Count == 0 ? "没有匹配的剪贴板记录" : "";
        OnPropertyChanged(nameof(SummaryText));
    }

    public void UpdateSelectedCount(int selectedCount)
    {
        if (_selectedCount == selectedCount)
        {
            return;
        }

        _selectedCount = selectedCount;
        OnPropertyChanged(nameof(SummaryText));
    }

    private bool MatchesSelectedFilter(ClipboardItem item)
    {
        return SelectedFilter switch
        {
            ClipboardOverlayFilter.Pinned => item.IsPinned,
            ClipboardOverlayFilter.Favorites => item.IsFavorite,
            ClipboardOverlayFilter.Text => string.Equals(item.ContentType, "text", StringComparison.OrdinalIgnoreCase),
            ClipboardOverlayFilter.Images => string.Equals(item.ContentType, "image/png", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private CancellationTokenSource ReplaceSearchCancellation()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _searchCancellation, next);
        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        return next;
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

    public async Task<bool> CopySelectedAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        try
        {
            await _clipboardService.CopyItemsAsync(selectedItems, CancellationToken.None);
            StatusText = selectedItems.Count == 1 ? "已复制到剪贴板" : $"已合并复制 {selectedItems.Count} 条";
            await SearchAsync();
            return true;
        }
        catch (NotSupportedException ex)
        {
            StatusText = ex.Message;
            return false;
        }
    }

    public async Task<bool> DeleteItemsAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        var deleted = await _clipboardService.DeleteItemsAsync(selectedItems.Select(item => item.Id).ToArray(), CancellationToken.None);
        await SearchAsync();
        StatusText = $"已删除 {deleted} 条";
        return deleted > 0;
    }

    public async Task<bool> ToggleSelectedPinnedAsync()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        var nextPinned = !SelectedItem.IsPinned;
        await _clipboardService.SetPinnedAsync(SelectedItem.Id, nextPinned, CancellationToken.None);
        await SearchAsync();
        StatusText = nextPinned ? "已置顶" : "已取消置顶";
        return true;
    }

    public async Task<bool> ToggleSelectedFavoriteAsync()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        var nextFavorite = !SelectedItem.IsFavorite;
        await _clipboardService.SetFavoriteAsync(SelectedItem.Id, nextFavorite, CancellationToken.None);
        await SearchAsync();
        StatusText = nextFavorite ? "已保存为片段" : "已取消片段";
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum ClipboardOverlayFilter
{
    All,
    Pinned,
    Favorites,
    Text,
    Images
}

public sealed record ClipboardOverlayFilterOption(ClipboardOverlayFilter Filter, string Label);
