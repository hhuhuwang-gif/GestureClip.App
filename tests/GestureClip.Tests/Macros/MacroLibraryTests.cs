using System.Text;
using GestureClip.Core.Macros;
using GestureClip.Infrastructure.Macros;
using GestureClip.Infrastructure.Paths;
using Xunit;

namespace GestureClip.Tests.Macros;

public sealed class MacroLibraryTests
{
    private const int LargeEventCount = 100_000;
    private const int LongNameCharacterCount = 20_000;
    private static readonly DateTimeOffset RecordingTime = new(2026, 11, 17, 10, 23, 34, TimeSpan.FromHours(8));

    [Fact]
    public async Task Existing_files_display_the_recording_name_without_changing_the_file_path()
    {
        var store = CreateStore();
        var path = await store.SaveAsync(new RecordedMacro { Name = "  每日报表录制  ", CreatedAt = RecordingTime });
        var entry = Assert.Single(await store.ListEntriesAsync());
        Assert.Equal("每日报表录制", entry.DisplayName);
        Assert.Equal(path, entry.Path);
        Assert.True(File.Exists(path));
        Assert.Equal("  每日报表录制  ", (await store.LoadAsync(path)).Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unnamed_recordings_use_their_saved_time_instead_of_guid(string name)
    {
        var store = CreateStore();
        await store.SaveAsync(new RecordedMacro { Name = name, CreatedAt = RecordingTime });
        var entry = Assert.Single(await store.ListEntriesAsync());
        Assert.Equal("录制 · 2026-11-17 10:23:34", entry.DisplayName);
        Assert.Equal(RecordingTime, entry.CreatedAt);
    }

    [Theory]
    [InlineData("{\"Version\":1,\"Name\":null,\"CreatedAt\":\"2026-11-17T10:23:34+08:00\"}")]
    [InlineData("{\"Version\":2,\"Name\":\"录制\",\"CreatedAt\":\"2026-11-17T10:23:34+08:00\"}")]
    [InlineData("{\"Version\":1,\"Name\":\"录制\",\"CreatedAt\":\"invalid\"}")]
    [InlineData("{\"Version\":1,\"Name\":\"录制\"}")]
    [InlineData("{\"Version\":1,\"Name\":\"录制\",\"CreatedAt\":\"2026-11-17T10:23:34+08:00\",\"Events\":[}")]
    public async Task Invalid_metadata_is_reported_with_the_file_path(string content)
    {
        var store = CreateStore();
        Directory.CreateDirectory(store.DirectoryPath);
        var path = Path.Combine(store.DirectoryPath, "macro-invalid.json");
        await File.WriteAllTextAsync(path, content);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.ListEntriesAsync());
        Assert.Contains(path, error.Message);
    }

    [Fact]
    public async Task Metadata_scan_crosses_buffer_boundaries_without_deserializing_event_objects()
    {
        var store = CreateStore();
        Directory.CreateDirectory(store.DirectoryPath);
        var path = Path.Combine(store.DirectoryPath, "macro-large-old.json");
        await using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            await writer.WriteAsync("{\"Events\":[");
            for (var index = 0; index < LargeEventCount; index++)
            {
                if (index > 0) await writer.WriteAsync(",");
                // Deliberately not a valid MacroEvent: the library reads metadata only.
                await writer.WriteAsync("{\"Unrelated\":12345}");
            }
            await writer.WriteAsync(",null],\"Version\":1,\"Name\":\"旧录制\",\"CreatedAt\":\"2026-11-17T10:23:34+08:00\"}");
        }
        var entry = Assert.Single(await store.ListEntriesAsync());
        Assert.Equal("旧录制", entry.DisplayName);
        Assert.Equal(path, entry.Path);
    }

    [Fact]
    public async Task Metadata_values_longer_than_the_read_buffer_are_preserved()
    {
        var store = CreateStore();
        var name = new string('录', LongNameCharacterCount);
        await store.SaveAsync(new RecordedMacro { Name = name, CreatedAt = RecordingTime });
        Assert.Equal(name, Assert.Single(await store.ListEntriesAsync()).DisplayName);
    }

    [Fact]
    public async Task Cancellation_is_not_converted_into_an_empty_library()
    {
        var store = CreateStore();
        await store.SaveAsync(new RecordedMacro { Name = "录制" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ListEntriesAsync(cancellation.Token));
    }

    private static MacroFileStore CreateStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "GestureClipMacroLibraryTests", Guid.NewGuid().ToString("N"));
        return new MacroFileStore(new AppPathProvider(root));
    }
}
