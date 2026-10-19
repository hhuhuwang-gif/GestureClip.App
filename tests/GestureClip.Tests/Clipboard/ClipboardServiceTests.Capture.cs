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
    public async Task CaptureTextAsync_skips_when_capture_is_disabled()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        await service.SetCaptureEnabledAsync(false, CancellationToken.None);

        await service.CaptureTextAsync(Capture("hello"), CancellationToken.None);

        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CaptureTextAsync_skips_empty_blacklisted_duplicate_and_sensitive_text()
    {
        var repository = new FakeClipboardRepository { BlockedProcessName = "vault.exe" };
        var service = CreateService(repository);

        await service.CaptureTextAsync(Capture("   "), CancellationToken.None);
        await service.CaptureTextAsync(Capture("secret", process: "vault.exe"), CancellationToken.None);
        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        await service.CaptureTextAsync(Capture("验证码 123456"), CancellationToken.None);

        Assert.Single(repository.Items);
        Assert.Equal("normal", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task CaptureTextAsync_keeps_plain_numeric_text_that_is_not_contextual_otp()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);

        await service.CaptureTextAsync(Capture("123456"), CancellationToken.None);

        Assert.Equal("123456", Assert.Single(repository.Items).TextContent);
    }

    [Fact]
    public async Task CaptureTextAsync_records_copy_count_after_successful_capture()
    {
        var repository = new FakeClipboardRepository();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(repository, dashboard: dashboard);

        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);

        await WaitForAsync(() => dashboard.CopyCount == 1);
        Assert.Equal(1, dashboard.CopyCount);
    }

    [Fact]
    public async Task CaptureTextAsync_refreshes_existing_duplicate_item()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);

        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        var existing = Assert.Single(repository.Items);
        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);

        Assert.Single(repository.Items);
        Assert.Equal(existing.Id, repository.TouchedId);
    }

    [Fact]
    public async Task HistoryRevision_changes_after_persisted_capture_and_duplicate_touch()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);

        Assert.Equal(0, service.HistoryRevision);

        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        Assert.Equal(1, service.HistoryRevision);

        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        Assert.Equal(2, service.HistoryRevision);

        await service.CaptureTextAsync(Capture("   "), CancellationToken.None);
        Assert.Equal(2, service.HistoryRevision);
    }

    [Fact]
    public async Task CaptureTextAsync_resumes_after_suppress_window_expires()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        service.SuppressCaptureFor(TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        await service.CaptureTextAsync(Capture("external copy"), CancellationToken.None);

        Assert.Single(repository.Items);
        Assert.Equal("external copy", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task CaptureTextAsync_allows_user_copy_during_internal_suppress_window_when_content_differs()
    {
        // Internal write "A" sets the fingerprint; a user copy "B" inside the same window
        // must NOT be swallowed just because the time window is still open.
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);

        await service.CopyItemsAsync([Item("A")], CancellationToken.None);
        Assert.True(service.SuppressCaptureUntil > DateTimeOffset.UtcNow);
        await service.CaptureTextAsync(Capture("B"), CancellationToken.None);

        var saved = Assert.Single(repository.Items);
        Assert.Equal("B", saved.TextContent);
    }

    [Fact]
    public async Task CaptureTextAsync_swallows_only_the_internal_write_echo_once()
    {
        // Internal write "A" → its own echo "A" must not create a duplicate history entry.
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);

        await service.CopyItemsAsync([Item("A")], CancellationToken.None);
        await service.CaptureTextAsync(Capture("A"), CancellationToken.None);

        Assert.Empty(repository.Items);
    }
}
