using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Database;

public sealed class DatabaseInitializer
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly SqlMigrationRunner _migrationRunner;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ISqliteConnectionFactory connectionFactory,
        SqlMigrationRunner migrationRunner,
        ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _migrationRunner = migrationRunner;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await _migrationRunner.RunAsync(connection, new[]
        {
            new SqlMigration(1, "initial", InitialMigration.Sql),
            new SqlMigration(2, "workstation_stats", WorkstationStatsMigration.Sql),
            new SqlMigration(3, "clipboard_performance_indexes", ClipboardPerformanceMigration.Sql),
            new SqlMigration(4, "clipboard_performance_indexes_v2", ClipboardPerformanceV2Migration.Sql),
            new SqlMigration(5, "clipboard_image_thumbnails", ClipboardThumbnailMigration.Sql)
        }, cancellationToken);

        await WorkstationHubMigration.EnsureAsync(connection);

        await _migrationRunner.RunAsync(connection, new[]
        {
            new SqlMigration(6, "workstation_hub_stats", WorkstationHubMigration.Sql),
            new SqlMigration(7, "clipboard_ocr_text", ClipboardOcrMigration.Sql)
        }, cancellationToken);

        _logger.LogInformation("Database migrations initialized.");
    }
}
