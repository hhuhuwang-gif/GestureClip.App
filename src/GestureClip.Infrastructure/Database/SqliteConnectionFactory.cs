using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Database;

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<SqliteConnectionFactory> _logger;

    public SqliteConnectionFactory(DatabaseOptions options, ILogger<SqliteConnectionFactory> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={_options.DatabasePath}");
        await connection.OpenAsync(cancellationToken);

        try
        {
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA synchronous = NORMAL;", cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _logger.LogInformation("SQLite connection opened with required PRAGMA settings.");
        return connection;
    }

    private static async Task ExecutePragmaAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
