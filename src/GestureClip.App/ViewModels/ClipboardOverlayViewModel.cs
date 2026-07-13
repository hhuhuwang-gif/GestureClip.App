using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed class ClipboardOverlayViewModel : INotifyPropertyChanged
{
    private const int PageSize = 50;
    private const int SelectedImagePreviewPixelWidth = 320;
    private const int MaxInlineImagePreviewBytes = 512 * 1024;

    private readonly IClipboardService _clipboardService;
    private readonly TimeSpan _searchDebounceDelay;
    private string _searchText = "";
    private ClipboardItem? _selectedItem;
    private string _emptyStateText = "";
    private string _statusText = "";
    private CancellationTokenSource? _searchCancellation;
    private int _searchVersion;
    private int _selectedImagePreviewVersion;
    private int _selectedCount;
    private IReadOnlyList<ClipboardItem> _lastSearchResults = [];
    private ClipboardOverlayFilter _selectedFilter = ClipboardOverlayFilter.All;
    private bool _isLoading;
    private bool _isLoadingMore;
    private bool _hasMoreItems;
    private string? _errorMessage;
    private bool _isShortcutHelpVisible;
    private IReadOnlyList<ClipboardItem> _undoDeleteItems = [];

    public ClipboardOverlayViewModel(IClipboardService clipboardService, TimeSpan? searchDebounceDelay = null)
    {
        _clipboardService = clipboardService;
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(180);
        StatusText = "";
        RefreshShortcutHint();
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
            _ = SearchAsync();
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
            if (value is { } selected && NeedsImagePreview(selected))
            {
                _ = LoadSelectedImagePreviewAsync(selected);
            }
        }
    }

    public bool HasSelectedItem => SelectedItem is not null;

    public bool IsSelectedImage => SelectedItem?.IsImage == true;

    public bool IsSelectedText => SelectedItem?.IsText == true;

    public string SelectedContentTypeText => SelectedItem?.ContentTypeLabel ?? "-";

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

    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => Items.Count == 0 && !IsLoading;

    public string ShortcutHintText { get; private set; } =
        "Home 回顶 · End 到底 · Ctrl+Enter 粘贴不关 · Ctrl+Shift+C 纯文本 · ? 快捷键 · Esc 清搜索/关闭";

    public bool CanUndoDelete => _undoDeleteItems.Count > 0;

    public bool IsShortcutHelpVisible
    {
        get => _isShortcutHelpVisible;
        private set
        {
            if (_isShortcutHelpVisible == value)
            {
                return;
            }

            _isShortcutHelpVisible = value;
            OnPropertyChanged();
        }
    }

    public string ShortcutHelpText { get; } =
        """
        打开/关闭面板    Ctrl + `
        聚焦搜索         Ctrl + F
        分类筛选         Ctrl + 1~5
        复制             Ctrl + C
        纯文本复制       Ctrl + Shift + C
        粘贴并关闭       Enter
        粘贴不关闭       Ctrl + Enter
        置顶 / 片段      Ctrl + P / Ctrl + S
        删除             Delete
        撤销删除         Ctrl + Z（刚删过时）
        回顶 / 到底      Home / End
        重置视图         Esc（有搜索时）或「清空」
        快捷键速查       ? 或 F1
        """;

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

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set
        {
            if (_isLoadingMore == value)
            {
                return;
            }

            _isLoadingMore = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoadMore));
        }
    }

    public bool HasMoreItems
    {
        get => _hasMoreItems;
        private set
        {
            if (_hasMoreItems == value)
            {
                return;
            }

            _hasMoreItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoadMore));
        }
    }

    public bool CanLoadMore => HasMoreItems && !IsLoading && !IsLoadingMore;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task LoadAsync()
    {
        await SearchAsync();
        SelectLatestItem();
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
        SelectLatestItem();
        StatusText = "已清空搜索";
        return true;
    }

    /// <summary>
    /// Clear search; if search already empty, reset filter to All. Always select latest.
    /// </summary>
    public async Task<bool> ResetViewAsync()
    {
        var changed = false;
        if (!string.IsNullOrEmpty(_searchText))
        {
            _searchText = "";
            OnPropertyChanged(nameof(SearchText));
            changed = true;
        }

        if (_selectedFilter != ClipboardOverlayFilter.All)
        {
            _selectedFilter = ClipboardOverlayFilter.All;
            OnPropertyChanged(nameof(SelectedFilter));
            changed = true;
        }

        if (changed)
        {
            await SearchAsync();
        }

        SelectLatestItem();
        StatusText = changed ? "已重置视图" : "已回到最新";
        return true;
    }

    public void SelectLatestItem()
    {
        SelectedItem = Items.FirstOrDefault();
        UpdateSelectedCount(SelectedItem is null ? 0 : 1);
    }

    public void ToggleShortcutHelp()
    {
        IsShortcutHelpVisible = !IsShortcutHelpVisible;
    }

    public void HideShortcutHelp()
    {
        IsShortcutHelpVisible = false;
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
            IsLoading = true;
            ErrorMessage = null;
            var keyword = SearchText;
            var filter = ToContentFilter(SelectedFilter);
            results = await Task.Run(
                () => _clipboardService.SearchAsync(keyword, PageSize, 0, filter, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载剪贴板历史失败：{ex.Message}";
            StatusText = "加载失败";
            return;
        }
        finally
        {
            if (version == Volatile.Read(ref _searchVersion))
            {
                IsLoading = false;
            }
        }

        if (cancellationToken.IsCancellationRequested || version != Volatile.Read(ref _searchVersion))
        {
            return;
        }

        _lastSearchResults = results;
        HasMoreItems = results.Count == PageSize;
        ApplyFilter();
        StatusText = "";
    }

    public async Task<bool> LoadMoreAsync()
    {
        if (!CanLoadMore)
        {
            return false;
        }

        var version = Volatile.Read(ref _searchVersion);
        var cancellation = _searchCancellation?.Token ?? CancellationToken.None;
        var offset = _lastSearchResults.Count;
        try
        {
            IsLoadingMore = true;
            ErrorMessage = null;
            var keyword = SearchText;
            var filter = ToContentFilter(SelectedFilter);
            var nextPage = await Task.Run(
                () => _clipboardService.SearchAsync(keyword, PageSize, offset, filter, cancellation),
                cancellation);
            if (cancellation.IsCancellationRequested || version != Volatile.Read(ref _searchVersion))
            {
                return false;
            }

            var existingIds = _lastSearchResults.Select(item => item.Id).ToHashSet();
            _lastSearchResults = _lastSearchResults
                .Concat(nextPage.Where(item => existingIds.Add(item.Id)))
                .ToArray();
            HasMoreItems = nextPage.Count == PageSize;
            ApplyFilter(keepSelection: true);
            StatusText = nextPage.Count == 0 ? "没有更多记录了" : $"已加载 {nextPage.Count} 条";
            return nextPage.Count > 0;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载更多剪贴板历史失败：{ex.Message}";
            StatusText = "加载更多失败";
            return false;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void ApplyFilter(bool keepSelection = false)
    {
        var refreshWatch = Stopwatch.StartNew();
        var previousSelectedId = keepSelection ? SelectedItem?.Id : null;
        var filtered = _lastSearchResults.Where(MatchesSelectedFilter).ToArray();
        Items.Clear();
        foreach (var item in filtered)
        {
            Items.Add(item);
        }

        SelectedItem = previousSelectedId is { } id
            ? Items.FirstOrDefault(item => item.Id == id) ?? Items.FirstOrDefault()
            : Items.FirstOrDefault();
        UpdateSelectedCount(SelectedItem is null ? 0 : 1);
        EmptyStateText = Items.Count == 0 ? "没有匹配的剪贴板记录" : "";
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        refreshWatch.Stop();
        Trace.WriteLine($"ClipboardPerf UiRefreshDurationMs ElapsedMs={refreshWatch.ElapsedMilliseconds} ItemCount={Items.Count} Filter={SelectedFilter}");
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
            ClipboardOverlayFilter.Text => item.IsText,
            ClipboardOverlayFilter.Images => item.IsImage,
            _ => true
        };
    }

    private static ClipboardContentFilter ToContentFilter(ClipboardOverlayFilter filter)
    {
        return filter switch
        {
            ClipboardOverlayFilter.Pinned => ClipboardContentFilter.Pinned,
            ClipboardOverlayFilter.Favorites => ClipboardContentFilter.Favorites,
            ClipboardOverlayFilter.Text => ClipboardContentFilter.Text,
            ClipboardOverlayFilter.Images => ClipboardContentFilter.Images,
            _ => ClipboardContentFilter.All
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

    public async Task<bool> PasteSelectedAsync(bool keepOverlayOpen = false)
    {
        if (SelectedItem is null)
        {
            return false;
        }

        var ok = await TryPasteAsync(SelectedItem);
        if (ok)
        {
            StatusText = keepOverlayOpen ? "已粘贴（面板保持打开）" : "已粘贴";
        }

        return ok;
    }

    public async Task<bool> PasteByIndexAsync(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return false;
        }

        var ok = await TryPasteAsync(Items[index]);
        if (ok)
        {
            StatusText = "已粘贴";
        }

        return ok;
    }

    private async Task<bool> TryPasteAsync(ClipboardItem item)
    {
        try
        {
            ErrorMessage = null;
            await _clipboardService.PasteAsync(item, new PasteOptions(false), CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"粘贴剪贴板历史失败：{ex.Message}";
            StatusText = "粘贴失败";
            return false;
        }
    }

    public async Task<bool> CopySelectedAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        try
        {
            ErrorMessage = null;
            await _clipboardService.CopyItemsAsync(selectedItems, CancellationToken.None);
            MarkItemsUsed(selectedItems.Select(item => item.Id).ToArray());
            StatusText = selectedItems.Count == 1 && selectedItems[0].IsImage
                ? "图片已复制到系统剪贴板 · 面板未关闭"
                : selectedItems.Count == 1
                    ? "文字已复制到系统剪贴板 · 面板未关闭"
                    : $"已合并复制 {selectedItems.Count} 条 · 面板未关闭";
            return true;
        }
        catch (NotSupportedException ex)
        {
            StatusText = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"复制剪贴板历史失败：{ex.Message}";
            StatusText = selectedItems.Count == 1 && selectedItems[0].IsImage
                ? $"图片复制失败：{ex.Message}"
                : "复制失败";
            return false;
        }
    }

    public async Task<bool> CopySelectedAsPlainTextAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        if (selectedItems.Any(item => item.IsImage))
        {
            StatusText = "图片无法复制为纯文本，请改用「复制」";
            return false;
        }

        try
        {
            ErrorMessage = null;
            var plainItems = new List<ClipboardItem>(selectedItems.Count);
            foreach (var item in selectedItems)
            {
                var full = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
                var raw = full.TextContent ?? item.TextContent ?? item.PreviewText ?? "";
                var plain = CleanPlainText(raw);
                if (string.IsNullOrEmpty(plain))
                {
                    continue;
                }

                plainItems.Add(full with
                {
                    ContentType = "text",
                    TextContent = plain,
                    PreviewText = plain.Length > 120 ? plain[..120] : plain
                });
            }

            if (plainItems.Count == 0)
            {
                StatusText = "没有可复制的纯文本";
                return false;
            }

            await _clipboardService.CopyItemsAsync(plainItems, CancellationToken.None);
            MarkItemsUsed(plainItems.Select(item => item.Id).ToArray());
            StatusText = plainItems.Count == 1
                ? "已复制为纯文本 · 面板未关闭"
                : $"已合并复制 {plainItems.Count} 条纯文本 · 面板未关闭";
            return true;
        }
        catch (NotSupportedException ex)
        {
            StatusText = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"纯文本复制失败：{ex.Message}";
            StatusText = "纯文本复制失败";
            return false;
        }
    }

    public async Task<bool> DeleteItemsAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        // Prefer full content for undo (images / long text).
        var stash = new List<ClipboardItem>(selectedItems.Count);
        foreach (var item in selectedItems)
        {
            var full = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
            stash.Add(full);
        }

        var ids = stash.Select(item => item.Id).ToArray();
        var deleted = await _clipboardService.DeleteItemsAsync(ids, CancellationToken.None);
        if (deleted <= 0)
        {
            StatusText = "删除失败";
            return false;
        }

        _undoDeleteItems = stash;
        OnPropertyChanged(nameof(CanUndoDelete));
        RefreshShortcutHint();
        RemoveItems(ids);
        StatusText = deleted == 1
            ? "已删除 1 条 · Ctrl+Z 撤销"
            : $"已删除 {deleted} 条 · Ctrl+Z 撤销";
        return true;
    }

    public async Task<bool> UndoLastDeleteAsync()
    {
        if (_undoDeleteItems.Count == 0)
        {
            return false;
        }

        var items = _undoDeleteItems;
        try
        {
            await _clipboardService.RestoreItemsAsync(items, CancellationToken.None);
            _undoDeleteItems = [];
            OnPropertyChanged(nameof(CanUndoDelete));
            RefreshShortcutHint();
            await SearchAsync();
            SelectedItem = Items.FirstOrDefault(item => item.Id == items[0].Id) ?? Items.FirstOrDefault();
            UpdateSelectedCount(SelectedItem is null ? 0 : 1);
            StatusText = items.Count == 1
                ? "已撤销删除"
                : $"已撤销删除 {items.Count} 条";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"撤销删除失败：{ex.Message}";
            StatusText = "撤销失败";
            return false;
        }
    }

    private void RefreshShortcutHint()
    {
        ShortcutHintText = CanUndoDelete
            ? "Ctrl+Z 撤销删除 · Home 回顶 · Ctrl+Enter 粘贴不关 · ? 快捷键"
            : "Home 回顶 · End 到底 · Ctrl+Enter 粘贴不关 · Ctrl+Shift+C 纯文本 · ? 快捷键 · Esc 清搜索/关闭";
        OnPropertyChanged(nameof(ShortcutHintText));
    }

    private static string CleanPlainText(string text)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var result = new List<string>(lines.Length);
        var blankCount = 0;

        foreach (var line in lines)
        {
            var cleanedLine = line.TrimEnd();
            if (cleanedLine.Length == 0)
            {
                blankCount++;
                if (blankCount <= 1)
                {
                    result.Add("");
                }

                continue;
            }

            blankCount = 0;
            result.Add(cleanedLine);
        }

        while (result.Count > 0 && result[0].Length == 0)
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join("\r\n", result);
    }

    public async Task<bool> ToggleSelectedPinnedAsync()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        var nextPinned = !SelectedItem.IsPinned;
        var id = SelectedItem.Id;
        await _clipboardService.SetPinnedAsync(id, nextPinned, CancellationToken.None);
        UpdateItem(id, item => item with { IsPinned = nextPinned, UpdatedAt = DateTimeOffset.UtcNow });
        ApplyFilter();
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
        var id = SelectedItem.Id;
        await _clipboardService.SetFavoriteAsync(id, nextFavorite, CancellationToken.None);
        UpdateItem(id, item => item with { IsFavorite = nextFavorite, UpdatedAt = DateTimeOffset.UtcNow });
        ApplyFilter();
        StatusText = nextFavorite ? "已保存为片段" : "已取消片段";
        return true;
    }

    private void MarkItemsUsed(IReadOnlyList<Guid> ids)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var id in ids)
        {
            UpdateItem(id, item => item with
            {
                UseCount = item.UseCount + 1,
                LastUsedAt = now,
                UpdatedAt = now
            });
        }
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

    private async Task LoadSelectedImagePreviewAsync(ClipboardItem item)
    {
        var version = Interlocked.Increment(ref _selectedImagePreviewVersion);
        try
        {
            var fullItem = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None);
            if (fullItem is null || version != Volatile.Read(ref _selectedImagePreviewVersion))
            {
                return;
            }

            var previewContent = await GetImagePreviewContentAsync(fullItem, cancellationToken: CancellationToken.None);
            if (string.IsNullOrWhiteSpace(previewContent) || SelectedItem?.Id != item.Id)
            {
                return;
            }

            UpdateItem(item.Id, current => current with
            {
                TextContent = current.IsImage ? null : current.TextContent,
                ThumbnailContent = previewContent
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载图片预览失败：{ex.Message}";
        }
    }

    private static bool NeedsImagePreview(ClipboardItem? item)
    {
        return item is not null &&
            item.IsImage &&
            string.IsNullOrWhiteSpace(item.ThumbnailContent);
    }

    private static async Task<string?> GetImagePreviewContentAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.ThumbnailContent))
        {
            return item.ThumbnailContent;
        }

        if (!item.IsImage || string.IsNullOrWhiteSpace(item.TextContent))
        {
            return item.TextContent;
        }

        var thumbnail = await Task.Run(
            () => GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.TryCreateThumbnailPngBase64(
                item.TextContent,
                SelectedImagePreviewPixelWidth),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(thumbnail))
        {
            return thumbnail;
        }

        return EstimateBase64DecodedByteCount(item.TextContent) <= MaxInlineImagePreviewBytes
            ? item.TextContent
            : null;
    }

    private static long EstimateBase64DecodedByteCount(string value)
    {
        var base64 = NormalizeBase64(value);
        if (base64.Length == 0)
        {
            return 0;
        }

        var padding = base64.EndsWith("==", StringComparison.Ordinal)
            ? 2
            : base64.EndsWith("=", StringComparison.Ordinal)
                ? 1
                : 0;
        return (base64.Length * 3L / 4L) - padding;
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            trimmed = trimmed[(commaIndex + 1)..].Trim();
        }

        return trimmed.Any(char.IsWhiteSpace)
            ? new string(trimmed.Where(character => !char.IsWhiteSpace(character)).ToArray())
            : trimmed;
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
