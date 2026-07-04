using System.Text.Json;
using Dapper;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Database;

namespace GestureClip.Infrastructure.Settings;

public sealed class SqliteSettingsService : ISettingsService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, string?> _cache = [];

    public SqliteSettingsService(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public T Get<T>(string key, T defaultValue)
    {
        if (TryGetCached(key, out var cachedValue))
        {
            return Deserialize(cachedValue, defaultValue);
        }

        using var connection = _connectionFactory.OpenConnectionAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var value = connection.ExecuteScalar<string?>(
            "SELECT Value FROM Settings WHERE Key = @Key;",
            new { Key = key });

        lock (_cacheLock)
        {
            _cache[key] = value;
        }

        return Deserialize(value, defaultValue);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var serialized = JsonSerializer.Serialize(value);
        await connection.ExecuteAsync(
            """
INSERT INTO Settings (Key, Value, ValueType, UpdatedAt)
VALUES (@Key, @Value, @ValueType, @UpdatedAt)
ON CONFLICT(Key) DO UPDATE SET
    Value = excluded.Value,
    ValueType = excluded.ValueType,
    UpdatedAt = excluded.UpdatedAt;
""",
            new
            {
                Key = key,
                Value = serialized,
                ValueType = typeof(T).Name,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });

        lock (_cacheLock)
        {
            _cache[key] = serialized;
        }
    }

    private bool TryGetCached(string key, out string? value)
    {
        lock (_cacheLock)
        {
            return _cache.TryGetValue(key, out value);
        }
    }

    private static T Deserialize<T>(string? value, T defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value) ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }
}
