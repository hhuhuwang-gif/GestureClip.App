using System.Text.Json;
using System.IO;
using GestureClip.Core.Macros;
using GestureClip.Infrastructure.Paths;

namespace GestureClip.Infrastructure.Macros;

public sealed class MacroFileStore(AppPathProvider paths)
{
    public string DirectoryPath => Path.Combine(paths.RootDirectory, "macros");

    public async Task<string> SaveAsync(RecordedMacro macro)
    {
        macro.Validate();
        Directory.CreateDirectory(DirectoryPath);
        var name = $"macro-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
        var target = Path.Combine(DirectoryPath, name);
        var temporary = target + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                         bufferSize: 4096, useAsync: true))
            await JsonSerializer.SerializeAsync(stream, macro);
        File.Move(temporary, target);
        return target;
    }

    public async Task<RecordedMacro> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var macro = await JsonSerializer.DeserializeAsync<RecordedMacro>(stream)
            ?? throw new InvalidDataException("操作宏文件为空。");
        macro.Validate();
        return macro;
    }

    public IReadOnlyList<string> ListFiles() => Directory.Exists(DirectoryPath)
        ? Directory.GetFiles(DirectoryPath, "macro-*.json").OrderDescending(StringComparer.Ordinal).ToArray()
        : [];

    public async Task<IReadOnlyList<MacroLibraryEntry>> ListEntriesAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<MacroLibraryEntry>();
        foreach (var path in ListFiles())
            entries.Add(await MacroMetadataReader.ReadAsync(path, cancellationToken).ConfigureAwait(false));
        return entries;
    }
}
