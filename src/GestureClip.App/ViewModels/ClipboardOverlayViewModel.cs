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

public sealed partial class ClipboardOverlayViewModel : INotifyPropertyChanged
{
    [System.Diagnostics.Conditional("DEBUG")]
    private static void DebugPerf(string eventName, string detail = "")
    {
        var suffix = detail.Length == 0 ? string.Empty : $" {detail}";
        System.Diagnostics.Trace.WriteLine($"ClipboardPerf {eventName}{suffix}");
    }

    private const int PageSize = 50;
    private const int TextPreviewLength = 120;
    private const int SelectedImagePreviewPixelWidth = 320;
    private const int MaxInlineImagePreviewBytes = 512 * 1024;
    /// <summary>Pool size fetched for client-side pinyin-initial / regex matching.</summary>
    private const int SmartSearchScanLimit = 200;
    private const string RegexSearchPrefix = "re:";
    private const string DefaultShortcutHint =
        "拼音/re:正则 · Alt+数字粘贴 · Shift+Enter纯文本 · Ctrl+Enter粘贴不关 · Ctrl+Tab切分类";

    private readonly IClipboardService _clipboardService;
    private readonly IPasteQueueService? _pasteQueueService;
    private readonly TimeSpan _searchDebounceDelay;
    private string _searchText = "";
    private ClipboardItem? _selectedItem;
    private string _emptyStateText = "";
    private string _statusText = "";
    private CancellationTokenSource? _searchCancellation;
    private Task? _pendingSearch;
    private bool _isSearchDebouncing;
    private int _searchVersion;
    private int _selectedImagePreviewVersion;
    private int _selectedCount;
    private IReadOnlyList<ClipboardItem> _lastSearchResults = [];
    /// <summary>How many of <see cref="_lastSearchResults"/> came straight from repository paging
    /// (excludes client-side pinyin matches), so LoadMore keeps a correct offset.</summary>
    private int _primaryResultCount;
    private ClipboardOverlayFilter _selectedFilter = ClipboardOverlayFilter.All;
    private bool _isLoading;
    private bool _isLoadingMore;
    private bool _hasMoreItems;
    private bool _hasLoaded;
    private long _loadedHistoryRevision = -1;
    private readonly object _loadSync = new();
    private Task<bool>? _inFlightLoad;
    private long _inFlightLoadRevision = -1;
    private string? _errorMessage;
    private bool _isShortcutHelpVisible;
    private bool _isSnippetEditorOpen;
    private string _newSnippetText = "";
    private bool _isItemEditorOpen;
    private string _editingItemText = "";
    private Guid? _editingItemId;
    private bool _isQrOpen;
    private System.Windows.Media.ImageSource? _qrImageSource;
    private IReadOnlyList<ClipboardItem> _undoDeleteItems = [];
    /// <summary>Ids copied (or pasted) from the overlay in this process session.</summary>
    private readonly HashSet<Guid> _sessionCopiedIds = [];

    public ClipboardOverlayViewModel(
        IClipboardService clipboardService,
        TimeSpan? searchDebounceDelay = null,
        IPasteQueueService? pasteQueueService = null)
    {
        _clipboardService = clipboardService;
        _pasteQueueService = pasteQueueService;
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(180);
        StatusText = "";
        RefreshShortcutHint();
        if (_pasteQueueService is not null)
        {
            _pasteQueueService.QueueChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(PasteQueueCount));
                OnPropertyChanged(nameof(HasPasteQueue));
                OnPropertyChanged(nameof(PasteQueueText));
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RangeObservableCollection<ClipboardItem> Items { get; } = [];

    public IReadOnlyList<ClipboardOverlayFilterOption> FilterOptions { get; } =
    [
        new(ClipboardOverlayFilter.All, "全部"),
        new(ClipboardOverlayFilter.Pinned, "固定"),
        new(ClipboardOverlayFilter.Favorites, "片段"),
        new(ClipboardOverlayFilter.Text, "文本"),
        new(ClipboardOverlayFilter.Images, "图片"),
        new(ClipboardOverlayFilter.Links, "链接")
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
            _pendingSearch = SearchDebouncedAsync();
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

    public string SummaryText
    {
        get
        {
            var copiedOnScreen = 0;
            foreach (var item in Items)
            {
                if (item.IsSessionCopied)
                {
                    copiedOnScreen++;
                }
            }

            return copiedOnScreen > 0
                ? $"共 {Items.Count} 条 · 已选 {_selectedCount} 条 · 已复制 {copiedOnScreen} 条"
                : $"共 {Items.Count} 条 · 已选 {_selectedCount} 条";
        }
    }

    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => Items.Count == 0 && !IsLoading;

    public bool HasActiveQuery => !string.IsNullOrWhiteSpace(_searchText) || _selectedFilter != ClipboardOverlayFilter.All;

    public string EmptyStateTitle => HasActiveQuery ? "没有匹配的记录" : "还没有剪贴板记录";

    public string EmptyStateHint => HasActiveQuery
        ? "换个关键词，或点「全部」重置筛选试试"
        : "复制任意文字或截图后，会出现在这里";

}
