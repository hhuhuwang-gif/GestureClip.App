using Microsoft.Data.Sqlite;

namespace GestureClip.Infrastructure.Database;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
