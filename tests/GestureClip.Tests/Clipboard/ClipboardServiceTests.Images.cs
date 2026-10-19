using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardServiceTests
{

    [Fact]
    public async Task Clipboard_changed_captures_image_when_text_is_not_available()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { ImagePngBase64 = "png-base64" };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.Equal("image/png", repository.Items[0].ContentType);
        Assert.Equal("png-base64", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task Clipboard_changed_skips_image_when_base64_exceeds_configured_limit()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.ClipboardMaxImageBytes] = 128 * 1024;
        var reader = new FakeClipboardTextReader { ImagePngBase64 = Convert.ToBase64String(new byte[(128 * 1024) + 1]) };
        var service = CreateService(repository, listener: listener, reader: reader, settings: settings);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await Task.Delay(80);

        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Clipboard_changed_resizes_valid_large_image_instead_of_dropping_it()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.ClipboardMaxImageBytes] = 128 * 1024;
        var reader = new FakeClipboardTextReader { ImagePngBase64 = CreateNoisyPngBase64(512, 512) };
        var service = CreateService(repository, listener: listener, reader: reader, settings: settings);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        var item = Assert.Single(repository.Items);
        Assert.True(item.IsImage);
        Assert.True(Convert.FromBase64String(item.TextContent!).Length <= 128 * 1024);
    }

    [Fact]
    public async Task Clipboard_changed_prefers_explicit_image_over_text_fallback()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader
        {
            Snapshot = new ClipboardReadSnapshot("https://example.test/image.png", OnePixelPngBase64, PreferImage: true)
        };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.True(repository.Items[0].IsImage);
    }

    [Fact]
    public async Task Clipboard_changed_saves_png_image_with_thumbnail_contract()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { Text = null, ImagePngBase64 = OnePixelPngBase64 };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        var saved = Assert.Single(repository.Items);
        Assert.Equal("image/png", saved.ContentType);
        Assert.Equal(OnePixelPngBase64, saved.TextContent);
        Assert.False(string.IsNullOrWhiteSpace(saved.ThumbnailContent));
        Assert.False(string.IsNullOrWhiteSpace(saved.Hash));
    }

    [Fact]
    public async Task Clipboard_changed_image_only_copy_is_not_lost_when_text_read_is_null()
    {
        // Pure image clipboard (no text): the text-null path must fall through to the
        // image path instead of returning early.
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { Text = null, ImagePngBase64 = OnePixelPngBase64 };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.True(repository.Items[0].IsImage);
        Assert.Equal(OnePixelPngBase64, repository.Items[0].TextContent);
    }

    [Fact]
    public async Task Capture_duplicate_image_refreshes_existing_item_but_does_not_duplicate()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { Text = null, ImagePngBase64 = OnePixelPngBase64 };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);
        var existing = repository.Items[0];

        listener.Raise();
        await WaitForAsync(() => repository.TouchedId == existing.Id);

        Assert.Single(repository.Items);
        Assert.Equal(existing.Id, repository.TouchedId);
    }
}
