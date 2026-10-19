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

    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    private static ClipboardService CreateService(
        FakeClipboardRepository repository,
        FakeClipboardWriter? writer = null,
        FakeClipboardListener? listener = null,
        FakeClipboardTextReader? reader = null,
        FakeSettingsService? settings = null,
        FakeWorkstationDashboardService? dashboard = null)
    {
        return new ClipboardService(
            listener ?? new FakeClipboardListener(),
            reader ?? new FakeClipboardTextReader(),
            writer ?? new FakeClipboardWriter(),
            repository,
            new ClipboardHashService(),
            new SensitiveContentDetector(),
            new FakeForegroundAppService(),
            new FakeAppBlacklistService(repository),
            settings ?? new FakeSettingsService(),
            dashboard ?? new FakeWorkstationDashboardService(),
            NullLogger<ClipboardService>.Instance);
    }

    private static string CreateNoisyPngBase64(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        new Random(42).NextBytes(pixels);
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return Convert.ToBase64String(output.ToArray());
    }

    private static ClipboardCapture Capture(string text, string process = "notepad.exe")
    {
        return new ClipboardCapture(text, "Test", process, DateTimeOffset.UtcNow);
    }

    private static ClipboardItem Item(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(Guid.NewGuid(), "text", text, text, "hash", "plain", "Test", "test.exe", false, false, false, 0, now, now, null);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
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

            await Task.Delay(20);
        }

        Assert.True(condition());
    }
}
