using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

/// <summary>
/// Covers the smart-search layer of <see cref="ClipboardOverlayViewModel"/>:
/// pinyin-initial matching, "re:" regex search, the Links filter and text tools.
/// </summary>
public sealed class ClipboardOverlaySmartSearchTests
{
    [Fact]
    public async Task Search_finds_chinese_items_by_pinyin_initials()
    {
        var wechat = TextItem("微信聊天记录");
        var alipay = TextItem("支付宝账单");
        var service = new SmartSearchFakeService([wechat, alipay])
        {
            // Simulate the repository: substring search finds nothing for "wx".
            KeywordFilterEnabled = true
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = "wx";
        await WaitForAsync(() => viewModel.Items.Count == 1);

        Assert.Equal(wechat.Id, viewModel.Items.Single().Id);
        Assert.Contains("拼音首字母", viewModel.StatusText);
    }

    [Fact]
    public async Task Search_with_regex_prefix_filters_client_side()
    {
        var withCode = TextItem("验证码 483920，请勿泄露");
        var without = TextItem("普通文本，无数字串");
        var service = new SmartSearchFakeService([withCode, without]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = @"re:\d{6}";
        await WaitForAsync(() => viewModel.Items.Count == 1);

        Assert.Equal(withCode.Id, viewModel.Items.Single().Id);
        Assert.Contains("正则匹配", viewModel.StatusText);
        Assert.False(viewModel.HasMoreItems);
    }

    [Fact]
    public async Task Search_with_invalid_regex_reports_friendly_status()
    {
        var service = new SmartSearchFakeService([TextItem("anything")]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = "re:[";
        await WaitForAsync(() => viewModel.StatusText.Contains("正则表达式无效"));

        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task Links_filter_keeps_only_url_like_text_items()
    {
        var link = TextItem("https://example.com/docs");
        var www = TextItem("www.example.com");
        var plain = TextItem("just some text");
        var service = new SmartSearchFakeService([link, www, plain]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        viewModel.SelectedFilter = ClipboardOverlayFilter.Links;
        await WaitForAsync(() => viewModel.Items.Count == 2);

        Assert.Equal(
            new[] { link.Id, www.Id }.OrderBy(id => id),
            viewModel.Items.Select(item => item.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task TransformSelectedTextAsync_upper_cases_and_copies_result()
    {
        var item = TextItem("hello world");
        var service = new SmartSearchFakeService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var ok = await viewModel.TransformSelectedTextAsync(ClipboardTextTransform.UpperCase);

        Assert.True(ok);
        Assert.NotNull(service.LastCopiedItems);
        Assert.Equal("HELLO WORLD", service.LastCopiedItems![0].TextContent);
        Assert.Contains("大写", viewModel.StatusText);
    }

    [Fact]
    public async Task TransformSelectedTextAsync_formats_json()
    {
        var item = TextItem("{\"a\":1}");
        var service = new SmartSearchFakeService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var ok = await viewModel.TransformSelectedTextAsync(ClipboardTextTransform.FormatJson);

        Assert.True(ok);
        Assert.Contains("\"a\": 1", service.LastCopiedItems![0].TextContent);
    }

    [Fact]
    public async Task TransformSelectedTextAsync_rejects_invalid_json_without_copying()
    {
        var item = TextItem("not json at all");
        var service = new SmartSearchFakeService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var ok = await viewModel.TransformSelectedTextAsync(ClipboardTextTransform.FormatJson);

        Assert.False(ok);
        Assert.Null(service.LastCopiedItems);
        Assert.Contains("JSON", viewModel.StatusText);
    }

    private static ClipboardItem TextItem(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(
            Guid.NewGuid(),
            "text",
            text,
            text,
            $"hash-{Guid.NewGuid():N}",
            null,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class SmartSearchFakeService : IClipboardService
    {
        private readonly IReadOnlyList<ClipboardItem> _items;

        public SmartSearchFakeService(IReadOnlyList<ClipboardItem> items)
        {
            _items = items;
        }

        /// <summary>When true, mimics repository substring matching for non-empty keywords.</summary>
        public bool KeywordFilterEnabled { get; init; }

        public IReadOnlyList<ClipboardItem>? LastCopiedItems { get; private set; }

        public bool IsCaptureEnabled => true;

        public DateTimeOffset? SuppressCaptureUntil => null;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

        public void SuppressCaptureFor(TimeSpan duration)
        {
        }

        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<ClipboardItem> results = KeywordFilterEnabled && keyword.Length > 0
                ? _items.Where(item => (item.TextContent ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .ToArray()
                : _items.Take(limit).ToArray();
            return Task.FromResult(results);
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ClipboardItem?>(null);

        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
        {
            LastCopiedItems = items;
            return Task.CompletedTask;
        }

        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult(ids.Count);

        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
