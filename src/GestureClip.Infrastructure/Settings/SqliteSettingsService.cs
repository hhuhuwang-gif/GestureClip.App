using System.Text.Json;
using Dapper;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Database;

namespace GestureClip.Infrastructure.Settings;

public sealed class SqliteSettingsService : ISettingsService
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteSettingsService(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public T Get<T>(string key, T defaultValue)
    {
        using var connection = _connectionFactory.OpenConnectionAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var value = connection.ExecuteScalar<string?>(
            "SELECT Value FROM Settings WHERE Key = @Key;",
            new { Key = key });

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

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

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
                Value = JsonSerializer.Serialize(value),
                ValueType = typeof(T).Name,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });
    }
}
