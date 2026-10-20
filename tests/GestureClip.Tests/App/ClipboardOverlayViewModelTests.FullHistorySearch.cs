using GestureClip.App.ViewModels;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task Pinyin_search_includes_old_matches_even_with_a_full_literal_page()
    {
        const int historyCount = 450;
        var oldMatch = TextItem("微信旧记录");
        var rows = Enumerable.Range(0, historyCount)
            .Select(index => TextItem(index < 50 ? $"wx literal {index}" : $"ordinary {index}"))
            .Append(oldMatch).ToArray();
        var service = SearchableHistory(rows);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));

        viewModel.SearchText = "wx";
        await viewModel.SearchAsync();

        Assert.Contains(viewModel.Items, item => item.Id == oldMatch.Id);
        Assert.Contains("全部 451 条", viewModel.StatusText);
        Assert.Contains(400, service.SearchOffsets);
        Assert.True(viewModel.HasMoreItems);
    }

    [Fact]
    public async Task Regex_search_covers_old_history_and_ocr_when_preview_is_present()
    {
        var oldMatch = ImageItem("截图") with { OcrText = "invoice 483920" };
        var rows = Enumerable.Range(0, 400).Select(index => TextItem($"ordinary {index}"))
            .Append(oldMatch).ToArray();
        var service = SearchableHistory(rows);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));

        viewModel.SearchText = @"re:\d{6}";
        await viewModel.SearchAsync();

        Assert.Equal(oldMatch.Id, Assert.Single(viewModel.Items).Id);
        Assert.Contains("全部 401 条", viewModel.StatusText);
        Assert.False(viewModel.HasMoreItems);
    }

    [Fact]
    public async Task Changing_query_cancels_full_history_scan_and_keeps_new_results()
    {
        var rows = Enumerable.Range(0, 250).Select(index => TextItem($"ordinary {index}")).ToArray();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = SearchableHistory(rows);
        service.SearchHandler = async (keyword, limit, offset, token) =>
        {
            if (keyword.Length > 0) return rows.Where(item => item.TextContent!.Contains(keyword)).Take(limit).ToArray();
            if (offset == 0) return rows.Take(limit).ToArray();
            started.SetResult();
            using var registration = token.Register(() => canceled.TrySetResult());
            await Task.Delay(Timeout.Infinite, token);
            return [];
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));
        viewModel.SearchText = "re:ordinary";
        var originalSearch = viewModel.SearchAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.SearchText = "ordinary 249";
        await viewModel.SearchAsync();
        await originalSearch;
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(rows[249].Id, Assert.Single(viewModel.Items).Id);
        Assert.False(viewModel.HasSearchError);
        Assert.DoesNotContain("正在搜索", viewModel.StatusText);
    }

    [Fact]
    public async Task History_change_during_scan_is_reported_instead_of_claiming_complete_results()
    {
        var rows = Enumerable.Range(0, 250).Select(index => TextItem($"ordinary {index}")).ToArray();
        var service = SearchableHistory(rows);
        service.SearchHandler = (_, limit, offset, _) =>
        {
            if (offset > 0) service.HistoryRevision++;
            return Task.FromResult<IReadOnlyList<ClipboardItem>>(rows.Skip(offset).Take(limit).ToArray());
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));

        viewModel.SearchText = "re:ordinary";
        await viewModel.SearchAsync();

        Assert.True(viewModel.HasSearchError);
        Assert.Contains("历史已变更", viewModel.ErrorMessage);
        Assert.False(await viewModel.PasteSelectedAsync());
    }

    [Fact]
    public async Task Invalid_regex_sets_search_error_and_blocks_paste()
    {
        var viewModel = new ClipboardOverlayViewModel(SearchableHistory([TextItem("previous")]), TimeSpan.FromDays(1));
        await viewModel.LoadAsync();

        viewModel.SearchText = "re:[";
        await viewModel.SearchAsync();

        Assert.True(viewModel.HasSearchError);
        Assert.Empty(viewModel.Items);
        Assert.False(await viewModel.PasteSelectedAsync());
    }

    private static FakeClipboardService SearchableHistory(IReadOnlyList<ClipboardItem> rows)
    {
        return new FakeClipboardService(rows)
        {
            SearchHandler = (keyword, limit, offset, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<ClipboardItem>>(rows
                    .Where(item => keyword.Length == 0 || (item.TextContent ?? "").Contains(keyword))
                    .Skip(offset).Take(limit).ToArray());
            }
        };
    }

    [Fact]
    public async Task Regex_timeout_discards_partial_results_and_reports_failure()
    {
        var service = SearchableHistory([TextItem("aaa"), TextItem(new string('a', 32_000) + "!")]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));

        viewModel.SearchText = "re:^(a+)+$";
        await viewModel.SearchAsync();

        Assert.True(viewModel.HasSearchError);
        Assert.Contains("正则匹配超时", viewModel.StatusText);
        Assert.Empty(viewModel.Items);
        Assert.False(await viewModel.PasteSelectedAsync());
    }
}
