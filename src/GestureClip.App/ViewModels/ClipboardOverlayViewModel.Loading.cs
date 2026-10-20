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
    public Task<bool> LoadAsync(bool force = false)
    {
        var currentRevision = _clipboardService.HistoryRevision;
        if (!force && _hasLoaded && _loadedHistoryRevision == currentRevision)
        {
            DebugPerf("OVERLAY_CACHE_HIT", $"Revision={currentRevision}");
            return Task.FromResult(false);
        }

        lock (_loadSync)
        {
            if (!force &&
                _inFlightLoad is { IsCompleted: false } inFlight &&
                _inFlightLoadRevision == currentRevision)
            {
                DebugPerf("OVERLAY_LOAD_JOIN", $"Revision={currentRevision}");
                return inFlight;
            }

            _inFlightLoadRevision = currentRevision;
            return _inFlightLoad = LoadCoreAsync(force, currentRevision);
        }
    }

    private async Task<bool> LoadCoreAsync(bool force, long currentRevision)
    {
        DebugPerf("OVERLAY_LOAD_BEGIN", $"Force={force} Revision={currentRevision}");
        if (force) _smartSearchPool = null;
        _isSearchDebouncing = false;
        var cancellation = ReplaceSearchCancellation();
        var search = SearchCoreAsync(cancellation.Token);
        _pendingSearch = search;
        var refreshed = await search;
        if (!refreshed)
        {
            return false;
        }

        SelectLatestItem();
        DebugPerf("OVERLAY_READY");
        return true;
    }

    public Task SearchAsync()
    {
        _isSearchDebouncing = false;
        var cancellation = ReplaceSearchCancellation();
        return _pendingSearch = SearchCoreAsync(cancellation.Token);
    }

    public async Task FlushPendingSearchAsync()
    {
        while (_pendingSearch is { IsCompleted: false } pending)
        {
            if (_isSearchDebouncing)
            {
                await SearchAsync();
            }
            else
            {
                await pending;
            }
        }
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
        _isSearchDebouncing = true;
        var cancellation = ReplaceSearchCancellation();
        try
        {
            if (_searchDebounceDelay > TimeSpan.Zero)
            {
                await Task.Delay(_searchDebounceDelay, cancellation.Token);
            }

            _isSearchDebouncing = false;
            await SearchCoreAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

}
