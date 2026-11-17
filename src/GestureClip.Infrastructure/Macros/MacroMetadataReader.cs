using System.IO;
using System.Text.Json;
using GestureClip.Core.Macros;

namespace GestureClip.Infrastructure.Macros;

// Read metadata without materializing the potentially large Events array.
internal sealed class MacroMetadataReader
{
    private const int InitialBufferSize = 16 * 1024;
    private const int BufferGrowthFactor = 2;
    private JsonReaderState _state;
    private string? _property;
    private string? _name;
    private int? _version;
    private DateTimeOffset? _createdAt;
    private bool _rootObject;
    private bool _rootClosed;

    internal static async Task<MacroLibraryEntry> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                InitialBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var reader = new MacroMetadataReader();
            await reader.ReadStreamAsync(stream, cancellationToken).ConfigureAwait(false);
            return reader.CreateEntry(path);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"操作宏元数据无效：{path}。{error.Message}", error);
        }
    }

    private async Task ReadStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[InitialBufferSize];
        var buffered = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(buffered), cancellationToken).ConfigureAwait(false);
            buffered += read;
            var consumed = Consume(buffer.AsSpan(0, buffered), read == 0);
            buffered -= consumed;
            buffer.AsSpan(consumed, buffered).CopyTo(buffer);
            if (read == 0) return;
            if (buffered == buffer.Length) Array.Resize(ref buffer, buffer.Length * BufferGrowthFactor);
        }
    }

    private int Consume(ReadOnlySpan<byte> buffer, bool finalBlock)
    {
        var reader = new Utf8JsonReader(buffer, finalBlock, _state);
        while (reader.Read())
        {
            if (reader.CurrentDepth == 0) ReadRootToken(reader.TokenType);
            if (reader.CurrentDepth != 1) continue;
            if (reader.TokenType == JsonTokenType.PropertyName) { _property = reader.GetString(); continue; }
            ReadMetadataValue(ref reader);
            _property = null;
        }
        _state = reader.CurrentState;
        return checked((int)reader.BytesConsumed);
    }

    private void ReadRootToken(JsonTokenType type)
    {
        if (type == JsonTokenType.StartObject) { _rootObject = true; return; }
        if (type == JsonTokenType.EndObject) { _rootClosed = true; return; }
        throw new InvalidDataException("操作宏必须是 JSON 对象。");
    }

    private void ReadMetadataValue(ref Utf8JsonReader reader)
    {
        switch (_property)
        {
            case nameof(RecordedMacro.Version):
                if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var version))
                    throw new InvalidDataException("版本号必须是整数。");
                _version = version;
                break;
            case nameof(RecordedMacro.Name):
                if (reader.TokenType != JsonTokenType.String) throw new InvalidDataException("录制名称必须是字符串。");
                _name = reader.GetString();
                break;
            case nameof(RecordedMacro.CreatedAt):
                if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var createdAt))
                    throw new InvalidDataException("录制时间无效。");
                _createdAt = createdAt;
                break;
        }
    }

    private MacroLibraryEntry CreateEntry(string path)
    {
        if (!_rootObject || !_rootClosed || _version != 1 || _name is null || _createdAt is null)
            throw new InvalidDataException("不支持的版本或缺少录制名称、时间。");
        return new MacroLibraryEntry(path, _name, _createdAt.Value);
    }
}
