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
    public async Task Clipboard_perf_setting_is_not_reloaded_for_every_perf_event()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { Text = "normal" };
        var settings = new FakeSettingsService();
        var service = CreateService(repository, listener: listener, reader: reader, settings: settings);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.True(settings.GetCount(SettingKeys.ClipboardPerfLogEnabled) <= 1);
    }

    [Fact]
    public async Task StartAsync_and_StopAsync_are_idempotent()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var service = CreateService(repository, listener: listener);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, listener.StartCount);
        Assert.Equal(1, listener.StopCount);
    }

    [Fact]
    public void ClipboardService_exposes_stage10_performance_metric_names()
    {
        var source = string.Join(
            Environment.NewLine,
            RepositorySource.ReadClassSources(FindRepositoryFile("src", "GestureClip.Features", "Clipboard", "ClipboardService.cs")),
            RepositorySource.ReadClassSources(FindRepositoryFile("src", "GestureClip.App", "ViewModels", "ClipboardOverlayViewModel.cs")),
            File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs")));

        Assert.Contains("ClipboardCopyDurationMs", source);
        Assert.Contains("DbUpdateDurationMs", source);
        Assert.Contains("UiRefreshDurationMs", source);
        Assert.Contains("SearchDurationMs", source);
        Assert.Contains("ThumbnailDecodeDurationMs", source);
        Assert.Contains("HudUpdateDurationMs", source);
    }
}
