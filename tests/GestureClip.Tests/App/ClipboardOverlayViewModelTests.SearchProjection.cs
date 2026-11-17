using GestureClip.App.ViewModels;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task Regex_image_match_hydrates_only_matching_display_rows_and_preserves_copy_and_paste()
    {
        var match = ImageItem("截图") with { OcrText = "invoice 483920" };
        var other = ImageItem("不匹配截图") with { OcrText = "ordinary image" };
        var service = SearchableHistory([other, match]);
        service.FullItems[match.Id] = match with { TextContent = "original-image-base64" };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));
        viewModel.SearchText = @"re:\d{6}";

        await viewModel.SearchAsync();

        var displayed = Assert.Single(viewModel.Items);
        Assert.Equal(match.Id, displayed.Id);
        Assert.Equal(match.ThumbnailContent, displayed.ThumbnailContent);
        Assert.Null(displayed.TextContent);
        Assert.Equal([match.Id], Assert.Single(service.DisplayItemRequests));
        Assert.True(await viewModel.CopySelectedAsync([displayed]));
        Assert.True(await viewModel.PasteSelectedAsync());
        Assert.Equal(match.Id, Assert.Single(service.LastCopiedItems!).Id);
    }

    [Fact]
    public async Task Releasing_search_pool_keeps_current_results_and_reloads_only_on_next_smart_query()
    {
        var match = TextItem("微信聊天记录");
        var service = SearchableHistory([match]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));
        viewModel.SearchText = "wx";
        await viewModel.SearchAsync();
        var selected = viewModel.SelectedItem;
        var readsBeforeRelease = service.SearchOffsets.Count;

        viewModel.ReleaseSearchCache();

        Assert.Equal("wx", viewModel.SearchText);
        Assert.Same(selected, viewModel.SelectedItem);
        Assert.Equal(match.Id, Assert.Single(viewModel.Items).Id);
        Assert.Equal(readsBeforeRelease, service.SearchOffsets.Count);
        await viewModel.SearchAsync();
        Assert.True(service.SearchOffsets.Count > readsBeforeRelease);
        Assert.Equal(match.Id, Assert.Single(viewModel.Items).Id);
    }
}
